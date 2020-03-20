using Abb.CqrsEs.UnitTests.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Abb.CqrsEs.UnitTests
{
    public class AggregateRootTests
    {
        private const int _numberOfRandomizedEvents = 20;
        private readonly ITestOutputHelper _outputHelper;

        public AggregateRootTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task AggregateRoot_registers_all_event_handlers()
        {
            var aggregate = new Aggregate(Guid.NewGuid(), GetLogger());
            await aggregate.EmitEvent(new Event1(Guid.NewGuid(), 0, aggregate.Id));
            await aggregate.EmitEvent(new Event2(Guid.NewGuid(), 1, aggregate.Id));
            await aggregate.EmitEvent(new Event3(Guid.NewGuid(), 2, aggregate.Id));
            await aggregate.EmitEvent(new Event4(Guid.NewGuid(), 3, aggregate.Id));
            Assert.Equal(1, aggregate.Event1Invocations);
            Assert.Equal(1, aggregate.Event2Invocations);
            Assert.Equal(1, aggregate.Event3Invocations);
            Assert.Equal(1, aggregate.Event4Invocations);
            Assert.Equal(4, aggregate.Version);
        }

        [Fact]
        public async Task AggregateRoot_event_order_is_preserved()
        {
            var aggregate = new Aggregate(GetLogger());
            var events = GenerateRandomizedEvents(aggregate).ToList();
            await events.ForEachAsync(aggregate.EmitEvent);

            int version = 0;
            var pendingEvents = await aggregate.GetPendingChanges();
            Assert.Equal(_numberOfRandomizedEvents, pendingEvents.Length);
            Assert.Equal(aggregate.Version, _numberOfRandomizedEvents);
            foreach (var pendingEvent in pendingEvents)
            {
                var expected = events[version];
                ++version;
                Assert.Equal(version, pendingEvent.Version);
                Assert.Equal(expected.CorrelationId, pendingEvent.CorrelationId);
            }

            await aggregate.CommitChanges();
            Assert.Equal(0, aggregate.PendingChangesCount);
        }

        [Fact]
        public async Task AggregateRoot_load_from_history_reestablishes_state()
        {
            var aggregate = new Aggregate(Guid.NewGuid(), GetLogger());
            var events = GenerateRandomizedEvents(aggregate).ToList();
            await events.ForEachAsync(aggregate.EmitEvent);

            var pendingChanges = await aggregate.GetPendingChanges();
            await aggregate.CommitChanges();

            var testAggregate = new Aggregate(GetLogger());
            await testAggregate.LoadFromHistory(pendingChanges);

            Assert.Equal(aggregate.Id, testAggregate.Id);
            Assert.Equal(aggregate.Version, testAggregate.Version);
            Assert.Equal(aggregate.Event1Invocations, testAggregate.Event1Invocations);
            Assert.Equal(aggregate.Event2Invocations, testAggregate.Event2Invocations);
            Assert.Equal(aggregate.Event3Invocations, testAggregate.Event3Invocations);
            Assert.Equal(aggregate.Event4Invocations, testAggregate.Event4Invocations);
            Assert.Equal(aggregate.PendingChangesCount, testAggregate.PendingChangesCount);
        }

        [Fact]
        public async Task AggregateRoot_emit_event_fails_when_commit_is_in_progress()
        {
            var aggregate = new Aggregate(Guid.NewGuid(), GetLogger());
            var events = GenerateRandomizedEvents(aggregate).ToList();
            await events.SkipLast(1).ForEachAsync(aggregate.EmitEvent);

            var pendingChanges = await aggregate.GetPendingChanges();
            await Assert.ThrowsAsync<ConcurrencyException>(() => aggregate.EmitEvent(events.Last()));
        }

        [Fact]
        public async Task AggregateRoot_load_from_history_detects_event_stream_starts_with_invalid_version()
        {
            const int initialVersion = 5;
            var aggregate = new Aggregate(Guid.NewGuid(), initialVersion, GetLogger());
            var events = GenerateRandomizedEvents(aggregate).ToList();
            await events.ForEachAsync(aggregate.EmitEvent);

            var pendingChanges = await aggregate.GetPendingChanges();
            await aggregate.CommitChanges();
            var testAggregate = new Aggregate(GetLogger());
            await Assert.ThrowsAsync<EventStreamException>(async () => await testAggregate.LoadFromHistory(pendingChanges));
        }

        [Fact]
        public async Task AggregateRoot_load_from_history_detects_event_stream_has_invalid_versions()
        {
            const int initialVersion = 5;
            var aggregate = new Aggregate(Guid.NewGuid(), initialVersion, GetLogger());
            var events = GenerateRandomizedEvents(aggregate).ToList();
            await events.ForEachAsync(aggregate.EmitEvent);

            var pendingChanges = await aggregate.GetPendingChanges();
            var invalidEventStream = pendingChanges.Skip(1).Select(@event =>
            {
                var ctor = @event.GetType().GetTypeInfo().DeclaredConstructors.ToArray()[0];
                return (Event)ctor.Invoke(new object[] { @event.CorrelationId, @event.Version + 1, @event.AggregateId });
            });

            var testAggregate = new Aggregate(GetLogger());
            await Assert.ThrowsAsync<EventStreamException>(async () => await testAggregate.LoadFromHistory(invalidEventStream));
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
                    yield return new Event1(Guid.NewGuid(), nextVersion, aggregate.Id);
                }
                else if (number <= 20)
                {
                    yield return new Event2(Guid.NewGuid(), nextVersion, aggregate.Id);
                }
                else if (number <= 30)
                {
                    yield return new Event3(Guid.NewGuid(), nextVersion, aggregate.Id);
                }
                else
                {
                    yield return new Event4(Guid.NewGuid(), nextVersion, aggregate.Id);
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
            : this(Guid.Empty, logger)
            { }

            public Aggregate(Guid id, ILogger logger)
                : this(id, AggregateRoot.InitialVersion, logger)
            {
                Id = id;
            }

            public Aggregate(Guid id, int version, ILogger logger)
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

            public Task EmitEvent(Event @event)
            {
                return Emit(@event, CancellationToken.None);
            }

            private void Handle(Event1 @event)
            {
                Event1Invocations++;
            }

            private void Apply(Event2 @event, CancellationToken token)
            {
                Event2Invocations++;
            }

            private Task Handle(Event3 @event)
            {
                Event3Invocations++;
                return Task.CompletedTask;
            }

            private Task Apply(Event4 @event, CancellationToken token)
            {
                Event4Invocations++;
                return Task.CompletedTask;
            }
        }

        private class Event1 : EventWrapper
        {
            public Event1(Guid correlationId, int version, Guid aggregateId)
                : base(correlationId, version, aggregateId)
            {
            }
        }

        private class Event2 : EventWrapper
        {
            public Event2(Guid correlationId, int version, Guid aggregateId)
                : base(correlationId, version, aggregateId)
            {
            }
        }

        private class Event3 : EventWrapper
        {
            public Event3(Guid correlationId, int version, Guid aggregateId)
                : base(correlationId, version, aggregateId)
            {
            }
        }

        private class Event4 : EventWrapper
        {
            public Event4(Guid correlationId, int version, Guid aggregateId)
                : base(correlationId, version, aggregateId)
            {
            }
        }
    }
}