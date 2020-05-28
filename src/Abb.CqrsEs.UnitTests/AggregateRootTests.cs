using Abb.CqrsEs.UnitTests.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Abb.CqrsEs.UnitTests
{
    public class AggregateRootTests
    {
        private const string _aggregateId = "TestAggregate";
        private const int _numberOfRandomizedEvents = 20;
        private readonly ITestOutputHelper _outputHelper;

        public AggregateRootTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public void AggregateRoot_emit_event_fails_when_commit_is_in_progress()
        {
            var aggregate = new Aggregate(_aggregateId, GetLogger());
            var events = GenerateRandomizedEvents().ToList();
            events.SkipLast(1).ForEach(aggregate.EmitEvent);

            _ = aggregate.GetPendingChanges();
            Assert.Throws<ConcurrencyException>(() => aggregate.EmitEvent(events.Last()));
        }

        [Fact]
        public void AggregateRoot_event_order_is_preserved()
        {
            var aggregate = new Aggregate(GetLogger());
            var events = GenerateRandomizedEvents().ToList();
            events.ForEach(aggregate.EmitEvent);

            var pendingEvents = aggregate.GetPendingChanges();
            Assert.Equal(_numberOfRandomizedEvents, pendingEvents.Count());
            Assert.Equal(aggregate.Version, _numberOfRandomizedEvents);
            aggregate.CommitChanges();
            Assert.Equal(0, aggregate.PendingChangesCount);
        }

        [Fact]
        public void AggregateRoot_load_from_history_reestablishes_state()
        {
            var aggregate = new Aggregate(_aggregateId, GetLogger());
            var events = GenerateRandomizedEvents().ToList();
            events.ForEach(aggregate.EmitEvent);

            var pendingChanges = aggregate.GetPendingChanges();

            var testAggregate = new Aggregate(_aggregateId, GetLogger());
            testAggregate.Load(pendingChanges);

            aggregate.CommitChanges();

            Assert.Equal(aggregate.Version, testAggregate.Version);
            Assert.Equal(aggregate.Event1Invocations, testAggregate.Event1Invocations);
            Assert.Equal(aggregate.Event2Invocations, testAggregate.Event2Invocations);
            Assert.Equal(aggregate.Event3Invocations, testAggregate.Event3Invocations);
            Assert.Equal(aggregate.Event4Invocations, testAggregate.Event4Invocations);
            Assert.Equal(0, aggregate.PendingChangesCount);
            Assert.Equal(aggregate.PendingChangesCount, testAggregate.PendingChangesCount);
        }

        [Fact]
        public void AggregateRoot_registers_all_event_handlers()
        {
            var aggregate = new Aggregate(_aggregateId, GetLogger());
            aggregate.EmitEvent(new Event1());
            aggregate.EmitEvent(new Event2());
            aggregate.EmitEvent(new Event3());
            aggregate.EmitEvent(new Event4());
            Assert.Equal(1, aggregate.Event1Invocations);
            Assert.Equal(1, aggregate.Event2Invocations);
            Assert.Equal(1, aggregate.Event3Invocations);
            Assert.Equal(1, aggregate.Event4Invocations);
            Assert.Equal(4, aggregate.Version);
        }

        private IEnumerable<object> GenerateRandomizedEvents()
        {
            var random = new Random();
            for (int i = 0; i < _numberOfRandomizedEvents; i++)
            {
                var number = random.Next(1, 40);
                if (number <= 10)
                {
                    yield return new Event1();
                }
                else if (number <= 20)
                {
                    yield return new Event2();
                }
                else if (number <= 30)
                {
                    yield return new Event3();
                }
                else
                {
                    yield return new Event4();
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

            public int UnknownEventInvocations { get; set; }

            protected override ILogger Logger => _logger;

            public void EmitEvent(object @event) => Emit(@event);

            protected override void When(object @event)
                => _ = @event switch
                {
                    Event1 _ => Event1Invocations++,
                    Event2 _ => Event2Invocations++,
                    Event3 _ => Event3Invocations++,
                    Event4 _ => Event4Invocations++,
                    _ => UnknownEventInvocations++
                };
        }

        private class Event1 { }

        private class Event2 { }

        private class Event3 { }

        private class Event4 { }
    }
}