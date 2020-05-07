using Abb.CqrsEs.UnitTests.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Abb.CqrsEs.UnitTests
{
    public class AggregateRootTests
    {
        private const int _numberOfRandomizedEvents = 20;
        private const string _aggregateId = "TestAggregate";
        private readonly ITestOutputHelper _outputHelper;

        public AggregateRootTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public void AggregateRoot_emit_event_fails_when_commit_is_in_progress()
        {
            var aggregate = new Aggregate(_aggregateId, GetLogger());
            var events = GenerateRandomizedEvents(aggregate).ToList();
            events.SkipLast(1).ForEach(aggregate.EmitEvent);

            _ = aggregate.GetPendingChanges();
            Assert.Throws<ConcurrencyException>(() => aggregate.EmitEvent(events.Last()));
        }

        [Fact]
        public void AggregateRoot_event_order_is_preserved()
        {
            var aggregate = new Aggregate(GetLogger());
            var events = GenerateRandomizedEvents(aggregate).ToList();
            events.ForEach(aggregate.EmitEvent);

            int version = 0;
            var pendingEvents = aggregate.GetPendingChanges();
            Assert.Equal(_numberOfRandomizedEvents, pendingEvents.Events.Count());
            Assert.Equal(aggregate.Version, _numberOfRandomizedEvents);
            foreach (var pendingEvent in pendingEvents.Events)
            {
                var expected = events[version];
                ++version;
                Assert.Equal(version, pendingEvent.Version);
                Assert.Equal(expected.CorrelationId, pendingEvent.CorrelationId);
            }

            aggregate.CommitChanges();
            Assert.Equal(0, aggregate.PendingChangesCount);
        }

        [Fact]
        public void AggregateRoot_load_from_history_detects_event_stream_has_invalid_versions()
        {
            const int initialVersion = 5;
            var aggregate = new Aggregate(_aggregateId, initialVersion, GetLogger());
            var events = GenerateRandomizedEvents(aggregate).ToList();
            events.ForEach(aggregate.EmitEvent);

            var pendingChanges = aggregate.GetPendingChanges();
            var invalidEventStream = new EventStream(aggregate.Id, pendingChanges.Events.Skip(1).Select(@event =>
            {
                var ctor = @event.GetType().GetTypeInfo().DeclaredConstructors.ToArray()[0];
                return (Event)ctor.Invoke(new object[] { @event.CorrelationId, @event.Version + 1 });
            }).ToArray());

            var testAggregate = new Aggregate(GetLogger());
            Assert.Throws<EventStreamException>(() => testAggregate.Load(invalidEventStream));
        }

        [Fact]
        public void AggregateRoot_load_from_history_detects_event_stream_starts_with_invalid_version()
        {
            const int initialVersion = 5;
            var aggregate = new Aggregate(_aggregateId, initialVersion, GetLogger());
            var events = GenerateRandomizedEvents(aggregate).ToList();
            events.ForEach(aggregate.EmitEvent);

            var pendingChanges = aggregate.GetPendingChanges();
            aggregate.CommitChanges();
            var testAggregate = new Aggregate(GetLogger());
            Assert.Throws<EventStreamException>(() => testAggregate.Load(pendingChanges));
        }

        [Fact]
        public void AggregateRoot_load_from_history_reestablishes_state()
        {
            var aggregate = new Aggregate(_aggregateId, GetLogger());
            var events = GenerateRandomizedEvents(aggregate).ToList();
            events.ForEach(aggregate.EmitEvent);

            var pendingChanges = aggregate.GetPendingChanges();
            aggregate.CommitChanges();

            var testAggregate = new Aggregate(GetLogger());
            testAggregate.Load(pendingChanges);

            Assert.Equal(aggregate.Id, testAggregate.Id);
            Assert.Equal(aggregate.Version, testAggregate.Version);
            Assert.Equal(aggregate.Event1Invocations, testAggregate.Event1Invocations);
            Assert.Equal(aggregate.Event2Invocations, testAggregate.Event2Invocations);
            Assert.Equal(aggregate.Event3Invocations, testAggregate.Event3Invocations);
            Assert.Equal(aggregate.Event4Invocations, testAggregate.Event4Invocations);
            Assert.Equal(aggregate.PendingChangesCount, testAggregate.PendingChangesCount);
        }

        [Fact]
        public void AggregateRoot_registers_all_event_handlers()
        {
            var aggregate = new Aggregate(_aggregateId, GetLogger());
            aggregate.EmitEvent(new Event1(Guid.NewGuid(), 0));
            aggregate.EmitEvent(new Event2(Guid.NewGuid(), 1));
            aggregate.EmitEvent(new Event3(Guid.NewGuid(), 2));
            aggregate.EmitEvent(new Event4(Guid.NewGuid(), 3));
            Assert.Equal(1, aggregate.Event1Invocations);
            Assert.Equal(1, aggregate.Event2Invocations);
            Assert.Equal(1, aggregate.Event3Invocations);
            Assert.Equal(1, aggregate.Event4Invocations);
            Assert.Equal(4, aggregate.Version);
        }

        private IEnumerable<Event> GenerateRandomizedEvents(AggregateRoot aggregate)
        {
            var random = new Random();
            for (int i = 0; i < _numberOfRandomizedEvents; i++)
            {
                var number = random.Next(1, 40);
                var nextVersion = aggregate.Version + i + 1;
                if (number <= 10)
                {
                    yield return new Event1(Guid.NewGuid(), nextVersion);
                }
                else if (number <= 20)
                {
                    yield return new Event2(Guid.NewGuid(), nextVersion);
                }
                else if (number <= 30)
                {
                    yield return new Event3(Guid.NewGuid(), nextVersion);
                }
                else
                {
                    yield return new Event4(Guid.NewGuid(), nextVersion);
                }
            }
        }

        private ILogger GetLogger()
        {
            var factory = new LoggerFactory();
            factory.AddProvider(new XunitLoggerProvider(_outputHelper));
            return factory.CreateLogger<AggregateRootTests>();
        }

        private class Aggregate : AggregateRoot
        {
            private readonly ILogger _logger;

            public Aggregate(ILogger logger)
            : this(string.Empty, logger)
            { }

            public Aggregate(string id, ILogger logger)
                : this(id, InitialVersion, logger)
            {
                Id = id;
            }

            public Aggregate(string id, int version, ILogger logger)
                : base()
            {
                Id = id;
                Version = version;
                _logger = logger;
            }

            public int Event1Invocations { get; set; }

            public int Event2Invocations { get; set; }

            public int Event3Invocations { get; set; }

            public int Event4Invocations { get; set; }

            protected override ILogger Logger => _logger;

            public void EmitEvent(Event @event) => Emit(@event);

            private void Event1Handler(Event1 _) => Event1Invocations++;

            private void Event2Handler(Event2 _) => Event2Invocations++;

            private void Event3Handler(Event3 _) => Event3Invocations++;

            private void Event4Handler(Event4 _) => Event4Invocations++;
        }

        private class Event1 : EventWrapper
        {
            public Event1(Guid correlationId, int version)
                : base(correlationId, version)
            {
            }
        }

        private class Event2 : EventWrapper
        {
            public Event2(Guid correlationId, int version)
                : base(correlationId, version)
            {
            }
        }

        private class Event3 : EventWrapper
        {
            public Event3(Guid correlationId, int version)
                : base(correlationId, version)
            {
            }
        }

        private class Event4 : EventWrapper
        {
            public Event4(Guid correlationId, int version)
                : base(correlationId, version)
            {
            }
        }
    }
}