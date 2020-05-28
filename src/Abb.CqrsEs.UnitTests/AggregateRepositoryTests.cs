using Abb.CqrsEs.UnitTests.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Abb.CqrsEs.UnitTests
{
    public class AggregateRepositoryTests
    {
        private readonly string _aggregateId = "Aggregate";
        private readonly int _numberOfEvents = 10;
        private readonly ITestOutputHelper _outputHelper;

        public AggregateRepositoryTests(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        [Fact]
        public async Task AggregateRepository_detect_concurrency_exception()
        {
            var aggregateId = _aggregateId;
            var aggregate = new Aggregate(aggregateId, GenerateRandomizedEvents(_numberOfEvents), GetLogger<Aggregate>());

            Assert.Equal(_numberOfEvents, aggregate.PendingChangesCount);

            var eventStore = new EventStore();
            eventStore.EventStreams.Add(new EventStream(aggregateId, 1, new[] { new Event1() }));

            var repository = new AggregateRepository(eventStore, new AggregateFactory(GetLogger<Aggregate>()), GetLogger<AggregateRepository>());
            await Assert.ThrowsAsync<ConcurrencyException>(() => repository.Save(aggregate, AggregateRoot.InitialVersion));
        }

        [Fact]
        public async Task AggregateRepository_get_restores_correct_state()
        {
            var aggregateId = _aggregateId;
            var eventStore = new EventStore();
            var eventStream = new EventStream(aggregateId, 1, GenerateRandomizedEvents(_numberOfEvents).ToArray());
            eventStore.EventStreams.Add(eventStream);

            var AggregateRepository = new AggregateRepository(eventStore, new AggregateFactory(GetLogger<Aggregate>()), GetLogger<AggregateRepository>());

            var aggregate = await AggregateRepository.Get<Aggregate>(aggregateId);

            Assert.Equal(_numberOfEvents, aggregate.Version);
            Assert.Equal(aggregateId, aggregate.Id);
        }

        [Fact]
        public async Task AggregateRepository_save_pending_changes()
        {
            var aggregateId = _aggregateId;
            var aggregate = new Aggregate(aggregateId, GenerateRandomizedEvents(_numberOfEvents), GetLogger<Aggregate>());

            Assert.Equal(_numberOfEvents, aggregate.PendingChangesCount);

            var eventStore = new EventStore();
            var repository = new AggregateRepository(eventStore, new AggregateFactory(GetLogger<Aggregate>()), GetLogger<AggregateRepository>());
            await repository.Save(aggregate, AggregateRoot.InitialVersion);

            Assert.Equal(0, aggregate.PendingChangesCount);
            Assert.Equal(_numberOfEvents, eventStore.EventStreams.Single(e => e.AggregateId == aggregateId).Events.Count());
        }

        private IEnumerable<object> GenerateRandomizedEvents(int numberOfRandomizedEvents)
        {
            var random = new Random();
            for (int i = 0; i < numberOfRandomizedEvents; i++)
            {
                var number = random.Next(1, 20);
                yield return number <= 10 ? new Event1() : (object)new Event2();
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

            public Aggregate(string id, IEnumerable<object> events, ILogger logger)
            {
                Id = id;
                events.ForEach(e => Emit(e));
                _logger = logger;
            }

            public int Event1Invocations { get; set; }

            public int Event2Invocations { get; set; }

            protected override ILogger Logger => _logger;

            protected override void When(object @event)
                => _ = @event switch
                {
                    Event1 _ => Event1Invocations++,
                    Event2 _ => Event2Invocations++,
                    _ => -1
                };
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

        private class Event1 { }

        private class Event2 { }

        private class EventStore : IEventStore
        {
            public IList<EventStream> EventStreams { get; } = new List<EventStream>();

            public Task<EventStream> GetEventStream(string aggregateId, int fromVersion, CancellationToken cancellationToken = default)
                => new EventStream(aggregateId, fromVersion, EventStreams.Where(e => e.AggregateId == aggregateId)
                    .SelectMany(e => e.Events)
                    .Skip(Math.Max(0, fromVersion - 1))
                    .ToArray())
                    .AsTask();

            public async Task<int> GetVersion(string aggregateId, CancellationToken cancellationToken = default) => (await GetEventStream(aggregateId, 1))?.Events.Count() ?? AggregateRoot.InitialVersion;

            public Task SaveAndPublish(EventStream eventStream, Action commit, CancellationToken cancellationToken = default)
            {
                var currentStream = EventStreams.SingleOrDefault(e => e.AggregateId == eventStream.AggregateId);
                if (currentStream == null)
                {
                    EventStreams.Add(new EventStream(eventStream.AggregateId, 1, eventStream.Events.ToArray()));
                }
                else
                {
                    EventStreams.Remove(currentStream);
                    EventStreams.Add(new EventStream(eventStream.AggregateId, 1, currentStream.Events.Concat(eventStream.Events).ToArray()));
                }
                commit();
                return Task.CompletedTask;
            }
        }
    }
}