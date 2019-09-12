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
    public class EventStoreTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public EventStoreTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
        }

        [Fact]
        public async Task EventStore_save_and_publish_handles_all_events()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence();
            var cache = new SimpleEventCache();
            var eventStore = new EventStore(cache, loggerFactory.CreateLogger<EventStore>(), () => publisher);

            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid();
            var events = new Event[] { new Event1(correlationId, 1, aggregateId), new Event1(correlationId, 2, aggregateId) };
            await eventStore.SaveAndPublish(aggregateId, events, _ => Task.CompletedTask, persistence, publisher);
            await Task.Delay(100);
            eventStore.Dispose();

            Assert.Equal(events.Length, publisher.PublishedEvents.Count);
            Assert.Equal(events.Length, persistence.PersistedEvents.Length);
        }

        [Fact]
        public async Task EventStore_save_and_publish_publish_fails()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence();
            var cache = new SimpleEventCache();
            var eventStore = new EventStore(cache, loggerFactory.CreateLogger<EventStore>(), () => publisher);

            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid();
            var events = new Event[] { new Event1(correlationId, 1, aggregateId), new EventFailingToPublish(correlationId, 2, aggregateId) };

            await eventStore.SaveAndPublish(aggregateId, events, _ => Task.CompletedTask, persistence, publisher);
            await Task.Delay(150);

            eventStore.Dispose();

            Assert.Equal(1, publisher.PublishedEvents.Count);
            Assert.Equal(events.Length, persistence.PersistedEvents.Length);
        }

        [Fact]
        public async Task EventStore_get_events_returns_saved_and_higher_state_events_only()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid();
            var events = new[] { new Event1(correlationId, 1, aggregateId), new Event1(correlationId, 2, aggregateId) };


            var persistence = new EventPersistence(events);
            var cache = new SimpleEventCache();
            var eventStore = new EventStore(cache, loggerFactory.CreateLogger<EventStore>(), () => new SimpleEventPublisher());

            var retrievedEvents = await eventStore.GetEvents(aggregateId, persistence);

            Assert.True(retrievedEvents.Any());
            Assert.Equal(events.Length, retrievedEvents.Count());
        }

        [Fact]
        public async Task EventStore_get_events_returns_empty_enumerable_for_unknown_id()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence();
            var cache = new SimpleEventCache();
            var eventStore = new EventStore(cache, loggerFactory.CreateLogger<EventStore>(), () => publisher);

            var retrievedEvents = await eventStore.GetEvents(Guid.NewGuid(), persistence);

            Assert.False(retrievedEvents.Any());
        }

        [Fact]
        public async Task EventStore_get_version_returns_correct_value()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid();
            var events = new[] { new Event1(correlationId, 1, aggregateId), new Event1(correlationId, 2, aggregateId) };


            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence(events);
            var cache = new SimpleEventCache();
            var eventStore = new EventStore(cache, loggerFactory.CreateLogger<EventStore>(), () => publisher);
            var version = await eventStore.GetVersion(aggregateId, persistence);
            eventStore.Dispose();
            Assert.Equal(2, version);
        }

        [Fact]
        public async Task EventStore_get_version_returns_default_for_unknown_id()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence();
            var cache = new SimpleEventCache();
            var eventStore = new EventStore(cache, loggerFactory.CreateLogger<EventStore>(), () => publisher);

            var version = await eventStore.GetVersion(Guid.NewGuid(), persistence);

            Assert.Equal(AggregateRoot.InitialVersion, version);
        }

        [Fact]
        public async Task EventStore_dispatches_and_publishes_correct_events_after_start()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid();
            var stream = new[]
            {
                new Event1(correlationId, 1, aggregateId), new Event1(correlationId, 2, aggregateId), new Event1(correlationId, 3, aggregateId)
            };

            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence();
            var cache = new SimpleEventCache(stream);

            var eventStore = new EventStore(cache, loggerFactory.CreateLogger<EventStore>(), () => publisher);
            await Task.Delay(150);
            eventStore.Dispose();

            Assert.Equal(3, publisher.PublishedEvents.Count);
        }

        private class Event1 : EventWrapper
        {
            public Event1(Guid correlationId, int version, Guid aggregateId) : base(correlationId, version, aggregateId)
            { }
        }

        private class EventFailingToSave : EventWrapper
        {
            public EventFailingToSave(Guid correlationId, int version, Guid aggregateId) : base(correlationId)
            { }
        }

        private class EventFailingToDispatch : EventWrapper
        {
            public EventFailingToDispatch(Guid correlationId, int version, Guid aggregateId) : base(correlationId)
            { }
        }

        private class EventFailingToPublish : EventWrapper
        {
            public EventFailingToPublish(Guid correlationId, int version, Guid aggregateId) : base(correlationId)
            { }
        }

        private class SimpleEventCache : IEventCache
        {
            public SimpleEventCache()
            { }

            public SimpleEventCache(IEnumerable<Event> preloadedStreams)
            {
                foreach (var eventsPerAggregate in preloadedStreams.GroupBy(e => e.AggregateId))
                {
                    var sortedEvents = eventsPerAggregate.OrderBy(e => e.Version).ToList();
                    CachedEvents.Add(eventsPerAggregate.Key, sortedEvents);
                }
            }

            public IDictionary<Guid, List<Event>> CachedEvents { get; } = new Dictionary<Guid, List<Event>>();

            public Task Add(Event @event, CancellationToken cancellationToken = default)
            {
                if (!CachedEvents.TryGetValue(@event.AggregateId, out var list))
                    CachedEvents[@event.AggregateId] = (list = new List<Event>());

                list.Add(@event);
                return Task.CompletedTask;
            }

            public Task<bool> Delete(Event @event, CancellationToken cancellationToken = default)
            {
                if (CachedEvents.TryGetValue(@event.AggregateId, out var list))
                    return Task.FromResult(list.Remove(@event));

                return Task.FromResult(false);
            }

            public Task<IEnumerable<IGrouping<Guid, Event>>> GetAll(CancellationToken cancellationToken = default)
            {
                return Task.FromResult(CachedEvents.Values.SelectMany(e => e).GroupBy(e => e.AggregateId));
            }
        }

        private class SimpleEventPublisher : IEventPublisher
        {
            public IList<IEvent> PublishedEvents { get; } = new List<IEvent>();

            public Task Publish(Event @event, CancellationToken token = default)
            {
                if (@event is EventFailingToPublish)
                    throw new InvalidOperationException();

                PublishedEvents.Add(@event);
                return Task.CompletedTask;
            }
        }

        private class EventPersistence : IEventPersistence
        {
            private readonly IList<Event> _persistedEvents;

            public EventPersistence()
            {
                _persistedEvents = new List<Event>();
            }

            public EventPersistence(IEnumerable<Event> events)
            {
                _persistedEvents = new List<Event>(events);
            }

            public Event[] PersistedEvents { get { return _persistedEvents.ToArray(); } }

            public Task<Event[]> Get(Guid aggregateId, int fromVersion = 0, CancellationToken token = default)
            {
                return _persistedEvents.Where(e => e.AggregateId == aggregateId && e.Version >= fromVersion)
                                       .ToArray()
                                       .AsTask();
            }

            public async Task<Event> GetLastOrDefault(Guid aggregateId, CancellationToken token = default)
            {
                return (await Get(aggregateId)).LastOrDefault();
            }

            public Task Save(IEnumerable<Event> events, CancellationToken token = default)
            {
                foreach (var @event in events)
                {
                    _persistedEvents.Add(@event);
                }
                return Task.CompletedTask;
            }
        }
    }
}