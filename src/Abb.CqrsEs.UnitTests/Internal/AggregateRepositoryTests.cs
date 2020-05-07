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
    public class AggregateRepositoryTests
    {
        private readonly int _numberOfEvents = 10;
        private readonly ITestOutputHelper _outputHelper;

        public AggregateRepositoryTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task AggregateRepository_detect_concurrency_exception()
        {
            var aggregateId = IdentityGenerator.GetNewIdentity();
            var aggregate = new Aggregate(aggregateId, GenerateRandomizedEvents(aggregateId, _numberOfEvents), GetLogger<Aggregate>());

            Assert.Equal(_numberOfEvents, aggregate.PendingChangesCount);

            var eventStore = new EventStore();
            eventStore.Events.Add(new Event1(Guid.NewGuid(), 1, aggregateId));

            var repository = new AggregateRepository(eventStore, new AggregateFactory(GetLogger<Aggregate>()), GetLogger<AggregateRepository>());
            await Assert.ThrowsAsync<ConcurrencyException>(() => repository.Save(aggregate, AggregateRoot.InitialVersion));
        }

        [Fact]
        public async Task AggregateRepository_get_restores_correct_state()
        {
            var aggregateId = IdentityGenerator.GetNewIdentity();
            var eventStore = new EventStore();
            GenerateRandomizedEvents(aggregateId, _numberOfEvents).ForEach(eventStore.Events.Add);

            var AggregateRepository = new AggregateRepository(eventStore, new AggregateFactory(GetLogger<Aggregate>()), GetLogger<AggregateRepository>());

            var aggregate = await AggregateRepository.Get<Aggregate>(aggregateId);

            Assert.Equal(_numberOfEvents, aggregate.Version);
            Assert.Equal(aggregateId, aggregate.Id);
        }

        [Fact]
        public async Task AggregateRepository_save_pending_changes()
        {
            var aggregateId = IdentityGenerator.GetNewIdentity();
            var aggregate = new Aggregate(aggregateId, GenerateRandomizedEvents(aggregateId, _numberOfEvents), GetLogger<Aggregate>());

            Assert.Equal(_numberOfEvents, aggregate.PendingChangesCount);

            var eventStore = new EventStore();
            var repository = new AggregateRepository(eventStore, new AggregateFactory(GetLogger<Aggregate>()), GetLogger<AggregateRepository>());
            await repository.Save(aggregate, AggregateRoot.InitialVersion);

            Assert.Equal(0, aggregate.PendingChangesCount);
            Assert.Equal(_numberOfEvents, eventStore.Events.Where(e => e.AggregateId == aggregateId).Count());
        }

        private IEnumerable<Event> GenerateRandomizedEvents(string aggregateId, int numberOfRandomizedEvents)
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

            public Aggregate(string id, IEnumerable<Event> events, ILogger logger)
            {
                Id = id;
                events.ForEachAsync(e => Emit(e, CancellationToken.None)).GetAwaiter().GetResult();
                _logger = logger;
            }

            public int Event1Invocations { get; set; }

            public int Event2Invocations { get; set; }

            protected override ILogger Logger => _logger;

            public Task EmitEvent(Event @event)
            {
                return Emit(@event, CancellationToken.None);
            }

            private void Apply(Event2 @event)
            {
                Event2Invocations++;
            }

            private void Handle(Event1 @event)
            {
                Event1Invocations++;
            }
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
                return Activator.CreateInstance(typeof(T), _logger) as T ?? throw new InvalidOperationException();
            }
        }

        private class Event1 : EventWrapper
        {
            public Event1(Guid correlationId, int version, string aggregateId)
                : base(correlationId, version, aggregateId)
            {
            }
        }

        private class Event2 : EventWrapper
        {
            public Event2(Guid correlationId, int version, string aggregateId)
                : base(correlationId, version, aggregateId)
            {
            }
        }

        private class EventStore : IEventStore
        {
            public IList<Event> Events { get; } = new List<Event>();

            public Task<IEnumerable<Event>> GetEventStream(string aggregateId, int fromVersion, CancellationToken token = default)
            {
                return Events.Where(e => e.AggregateId == aggregateId && e.Version >= fromVersion)
                              .OrderBy(e => e.Version)
                              .AsEnumerable()
                              .AsTask();
            }

            public Task<IEnumerable<Event>> GetEventStream(string aggregateId, CancellationToken token = default)
            {
                return GetEventStream(aggregateId, -1, token);
            }

            public async Task<int> GetVersion(string aggregateId, CancellationToken token = default)
            {
                var lastEvent = (await GetEventStream(aggregateId)).LastOrDefault();
                return lastEvent != null
                    ? lastEvent.Version
                    : 0;
            }

            public Task SaveAndPublish(EventStream eventStream, Func<CancellationToken, Task> beforePublish, CancellationToken token = default)
            {
                foreach (var @event in eventStream.Events)
                {
                    if (Events.Any(e => e.AggregateId == @event.AggregateId && e.Version == @event.Version))
                        throw new InvalidOperationException();
                }

                eventStream.Events.ForEach(Events.Add);
                return beforePublish(token);
            }
        }
    }
}