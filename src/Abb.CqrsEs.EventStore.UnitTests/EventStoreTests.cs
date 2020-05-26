using Abb.CqrsEs.UnitTests.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Abb.CqrsEs.EventStore.UnitTests
{
    public class EventStoreTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public EventStoreTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
        }

        [Fact]
        public async Task EventStore_get_events_returns_empty_enumerable_for_unknown_id()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence();
            var eventStore = new EventStore(persistence, publisher, loggerFactory.CreateLogger<EventStore>());

            var retrievedEvents = await eventStore.GetEventStream("invalid_aggragate_id");

            Assert.False(retrievedEvents.Events.Any());
        }

        [Fact]
        public async Task EventStore_get_events_returns_saved_and_higher_state_events_only()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid().ToString();
            var eventStream = new EventStream(aggregateId, new[]
            {
                new Event(correlationId, new Event1(), DateTimeOffset.UtcNow, 1),
                new Event(correlationId, new Event1(), DateTimeOffset.UtcNow, 2)
            });

            var persistence = new EventPersistence(new[] { eventStream });
            var eventStore = new EventStore(persistence, new SimpleEventPublisher(), loggerFactory.CreateLogger<EventStore>());

            var retrievedEvents = await eventStore.GetEventStream(aggregateId, default);

            Assert.True(retrievedEvents.Events.Any());
            Assert.Equal(eventStream.Events.Count(), retrievedEvents.Events.Count());
        }

        [Fact]
        public async Task EventStore_get_version_returns_correct_value()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid().ToString();
            var eventStream = new EventStream(aggregateId, new[]
            {
                new Event(correlationId, new Event1(), DateTimeOffset.UtcNow, 1),
                new Event(correlationId, new Event1(), DateTimeOffset.UtcNow, 2)
            });

            var persistence = new EventPersistence(new[] { eventStream });
            var eventStore = new EventStore(persistence, new SimpleEventPublisher(), loggerFactory.CreateLogger<EventStore>());
            var version = await eventStore.GetVersion(aggregateId, default);
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
            var eventStore = new EventStore(persistence, publisher, loggerFactory.CreateLogger<EventStore>());

            var version = await eventStore.GetVersion(Guid.NewGuid().ToString(), default);

            Assert.Equal(AggregateRoot.InitialVersion, version);
        }

        [Fact]
        public async Task EventStore_save_and_publish_handles_all_events()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence();
            var eventStore = new EventStore(persistence, publisher, loggerFactory.CreateLogger<EventStore>());

            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid().ToString();
            var eventStream = new EventStream(aggregateId, new[]
            {
                new Event(correlationId, new Event1(), DateTimeOffset.UtcNow, 1),
                new Event(correlationId, new Event1(), DateTimeOffset.UtcNow, 2)
            });

            await eventStore.SaveAndPublish(eventStream, () => { }, default);
            eventStore.Dispose();

            Assert.Equal(eventStream.Events.Count(), publisher.PublishedEvents.Count);
            Assert.Equal(eventStream.Events.Count(), persistence.GetPersistedEvents(aggregateId).Events.Count());
        }

        [Fact]
        public async Task EventStore_save_and_publish_publish_fails()
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence();
            var eventStore = new EventStore(persistence, publisher, loggerFactory.CreateLogger<EventStore>());

            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid().ToString();
            var eventStream = new EventStream(aggregateId, new[]
            {
                new Event(correlationId, new Event1(), DateTimeOffset.UtcNow, 1),
                new Event(correlationId, new EventFailingToPublish(), DateTimeOffset.UtcNow, 2)
            });

            await eventStore.SaveAndPublish(eventStream, () => { }, default);
            eventStore.Dispose();

            Assert.Equal(1, publisher.PublishedEvents.Count);
            Assert.Equal(eventStream.Events.Count(), persistence.GetPersistedEvents(aggregateId).Events.Count());
        }

        private class Event1 { }

        private class EventFailingToPublish { }

        private class EventPersistence : IEventPersistence
        {
            private readonly IDictionary<string, IList<Event>> _persistedEvents;

            public EventPersistence()
            {
                _persistedEvents = new Dictionary<string, IList<Event>>();
            }

            public EventPersistence(IEnumerable<EventStream> events)
            {
                _persistedEvents = new Dictionary<string, IList<Event>>();
                foreach (var group in events.GroupBy(s => s.AggregateId))
                {
                    _persistedEvents[group.Key] = new List<Event>(group.SelectMany(g => g.Events).OrderBy(e => e.Version));
                }
            }

            public IAsyncEnumerable<Event> Get(string aggregateId, int fromVersion = 0, CancellationToken cancellationToken = default)
                => _persistedEvents.TryGetValue(aggregateId, out var events)
                    ? events.Where(e => e.Version >= fromVersion).ToAsyncEnumerable()
                    : AsyncEnumerable.Empty<Event>();

            public async Task<Event> GetLastOrDefault(string aggregateId, CancellationToken cancellationToken = default)
                => await Get(aggregateId, 0, cancellationToken).LastOrDefaultAsync();

            public EventStream GetPersistedEvents(string aggregateId)
                => !_persistedEvents.ContainsKey(aggregateId)
                    ? new EventStream(aggregateId, Array.Empty<Event>())
                    : new EventStream(aggregateId, _persistedEvents[aggregateId].ToArray());

            public Task Save(string aggregateId, IEnumerable<Event> eventStream, CancellationToken cancellationToken = default)
            {
                if (!_persistedEvents.ContainsKey(aggregateId))
                {
                    _persistedEvents[aggregateId] = new List<Event>();
                }

                foreach (var @event in eventStream)
                {
                    _persistedEvents[aggregateId].Add(@event);
                }
                return Task.CompletedTask;
            }
        }

        private class SimpleEventPublisher : IEventPublisher
        {
            public IList<IEvent> PublishedEvents { get; } = new List<IEvent>();

            public Task Publish(EventStream eventStream, CancellationToken cancellationToken = default)
            {
                foreach (var @event in eventStream.Events)
                {
                    if (@event.Data is EventFailingToPublish)
                    {
                        throw new InvalidOperationException();
                    }

                    PublishedEvents.Add(@event);
                }
                return Task.CompletedTask;
            }
        }
    }
}