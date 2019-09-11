using Abb.CqrsEs.Internal;
using Abb.CqrsEs.UnitTests.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Abb.CqrsEs.UnitTests.Internal
{
    public class AggregateInteractionServiceTests
    {
        private readonly int _numberOfEvents = 10;
        private readonly ITestOutputHelper _outputHelper;

        public AggregateInteractionServiceTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task AggregateInteractionService_save_pending_changes()
        {
            var aggregateId = Guid.NewGuid();
            var aggregate = new Aggregate(aggregateId, GenerateRandomizedEvents(aggregateId, _numberOfEvents));

            Assert.Equal(_numberOfEvents, aggregate.PendingChangesCount);

            var eventStore = new EventStore();
            var repository = new AggregateInteractionService(eventStore, new AggregateFactory(GetLogger<Aggregate>()), null, new EventPublisher(), GetLogger<AggregateInteractionService>());
            await repository.Save(aggregate);

            Assert.Equal(0, aggregate.PendingChangesCount);
            Assert.Equal(_numberOfEvents, eventStore.Events.Where(e => e.AggregateId == aggregateId).Count());
        }

        [Fact]
        public async Task AggregateInteractionService_get_restores_correct_state()
        {
            var aggregateId = Guid.NewGuid();
            var eventStore = new EventStore();
            GenerateRandomizedEvents(aggregateId, _numberOfEvents).ForEach(eventStore.Events.Add);

            var AggregateInteractionService = new AggregateInteractionService(eventStore, new AggregateFactory(GetLogger<Aggregate>()), null, new EventPublisher(), GetLogger<AggregateInteractionService>());

            var aggregate = await AggregateInteractionService.Get<Aggregate>(aggregateId);

            Assert.Equal(_numberOfEvents, aggregate.Version);
            Assert.Equal(aggregateId, aggregate.Id);
        }

        [Fact]
        public async Task AggregateInteractionService_detect_concurrency_exception()
        {
            var aggregateId = Guid.NewGuid();
            var aggregate = new Aggregate(aggregateId, GenerateRandomizedEvents(aggregateId, _numberOfEvents));

            Assert.Equal(_numberOfEvents, aggregate.PendingChangesCount);

            var eventStore = new EventStore();
            eventStore.Events.Add(new Event1(Guid.NewGuid(), 1, aggregateId));

            var repository = new AggregateInteractionService(eventStore, new AggregateFactory(GetLogger<Aggregate>()), null, new EventPublisher(), GetLogger<AggregateInteractionService>());
            await Assert.ThrowsAsync<ConcurrencyException>(() => repository.Save(aggregate));
        }

        private IEnumerable<Event> GenerateRandomizedEvents(Guid aggregateId, int numberOfRandomizedEvents)
        {
            var random = new Random();
            for (int i = 0; i < numberOfRandomizedEvents; i++)
            {
                var number = random.Next(1, 20);
                var nextVersion = i + 1;
                if (number <= 10)
                    yield return new Event1(Guid.NewGuid(), nextVersion, aggregateId);
                else
                    yield return new Event2(Guid.NewGuid(), nextVersion, aggregateId);
            }
        }

        private ILogger<T> GetLogger<T>()
        {
            var factory = new LoggerFactory();
            factory.AddProvider(new XunitLoggerProvider(_outputHelper));
            return factory.CreateLogger<T>();
        }

        private class AggregateFactory : IAggregateFactory
        {
            private readonly ILogger<Aggregate> _logger;

            public AggregateFactory(ILogger<Aggregate> logger)
            {
                _logger = logger;
            }

            public T CreateAggregate<T>() where T : AggregateRoot
            {
                return (T)Activator.CreateInstance(typeof(T), _logger);
            }
        }

        private class Aggregate : AggregateRoot
        {
            private readonly ILogger _logger;

            public Aggregate(ILogger logger)
                : this(Guid.Empty, logger)
            { }

            public Aggregate(Guid id, ILogger logger)
                : this(id, InitialVersion, logger)
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

            public Aggregate(Guid id, IEnumerable<Event> events)
            {
                Id = id;
                events.ForEachAsync(e => Emit(e, CancellationToken.None)).GetAwaiter().GetResult();
            }

            public int Event1Invocations { get; set; }

            public int Event2Invocations { get; set; }

            protected override ILogger Logger => _logger;

            public Task EmitEvent(Event @event)
            {
                return Emit(@event, CancellationToken.None);
            }

            private void Handle(Event1 @event)
            {
                Event1Invocations++;
            }

            private void Apply(Event2 @event)
            {
                Event2Invocations++;
            }
        }

        private class EventStore : IEventStore
        {
            public IList<Event> Events { get; } = new List<Event>();

            public Task<IEnumerable<Event>> GetEvents(Guid aggregateId, IEventPersistence _, CancellationToken token = default)
            {
                return GetEvents(aggregateId, -1, _, token);
            }

            public Task<IEnumerable<Event>> GetEvents(Guid aggregateId, int fromVersion, IEventPersistence _, CancellationToken token = default)
            {
                return Events.Where(e => e.AggregateId == aggregateId && e.Version >= fromVersion)
                              .OrderBy(e => e.Version)
                              .AsEnumerable()
                              .AsTask();
            }

            public async Task<int> GetVersion(Guid aggregateId, IEventPersistence _, CancellationToken token = default)
            {
                var lastEvent = (await GetEvents(aggregateId, _)).LastOrDefault();
                return lastEvent != null
                    ? lastEvent.Version
                    : 0;
            }

            public Task SaveAndPublish(Guid aggregateId, IEnumerable<Event> events, Func<CancellationToken, Task> beforePublish, IEventPersistence _, IEventPublisher eventPublisher, CancellationToken token = default)
            {
                foreach (var @event in events)
                {
                    if (Events.Any(e => e.AggregateId == @event.AggregateId && e.Version == @event.Version))
                        throw new InvalidOperationException();
                }

                events.ForEach(Events.Add);
                return beforePublish(token);
            }
        }

        private class EventPublisher : IEventPublisher
        {
            public Task Publish(Event @event, CancellationToken cancellationToken = default)
            {
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
    }
}
