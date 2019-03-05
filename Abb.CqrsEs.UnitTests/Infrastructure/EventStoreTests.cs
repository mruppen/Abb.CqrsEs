using Abb.CqrsEs.Infrastructure;
using Abb.CqrsEs.UnitTests.Common;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Abb.CqrsEs.UnitTests.Infrastructure
{
    public class EventStoreTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public EventStoreTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper ?? throw new ArgumentNullException(nameof(testOutputHelper));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task EventStore_save_and_publish_handles_all_events(bool useBufferBlock)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var dispatcher = new SimpleEventDispatcher();
            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence();
            var eventStore = new EventStore(dispatcher, publisher, persistence, loggerFactory.CreateLogger<EventStore>(), useBufferBlock);

            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid();
            var events = new Event[] { new Event1(correlationId, 1, aggregateId), new Event1(correlationId, 2, aggregateId) };
            await eventStore.SaveAndPublish(events);
            await Task.Delay(100);
            eventStore.Dispose();

            Assert.Equal(events.Length, dispatcher.DispatchedEvents.Count);
            Assert.Equal(events.Length, publisher.PublishedEvents.Count);
            Assert.Equal(events.Length, persistence.PersistedEvents.Length);
            Assert.All(persistence.PersistedEvents, e => { if (e.State != PersistedEventImpl.EventState.Published) throw new Exception(); });
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task EventStore_save_and_publish_publish_fails(bool useBufferBlock)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var dispatcher = new SimpleEventDispatcher();
            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence();
            var eventStore = new EventStore(dispatcher, publisher, persistence, loggerFactory.CreateLogger<EventStore>(), useBufferBlock);

            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid();
            var events = new Event[] { new Event1(correlationId, 1, aggregateId), new EventFailingToPublish(correlationId, 2, aggregateId) };

            await eventStore.SaveAndPublish(events);
            await Task.Delay(150);

            eventStore.Dispose();

            Assert.Equal(events.Length, dispatcher.DispatchedEvents.Count);
            Assert.Equal(1, publisher.PublishedEvents.Count);
            Assert.Equal(events.Length, persistence.PersistedEvents.Length);
            Assert.Equal(PersistedEventImpl.EventState.Published, persistence.PersistedEvents[0].State);
            Assert.Equal(PersistedEventImpl.EventState.Dispatched, persistence.PersistedEvents[1].State);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task EventStore_get_events_returns_saved_and_higher_state_events_only(bool useBufferBlock)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid();
            var events = new[] { new Event1(correlationId, 1, aggregateId), new Event1(correlationId, 2, aggregateId) };
            var persistedEvents = events.Select(e => new PersistedEventImpl(e) { State = PersistedEventImpl.EventState.Published }).ToList();
            persistedEvents.Add(new PersistedEventImpl(new Event1(correlationId, 3, aggregateId)) { State = PersistedEventImpl.EventState.Idle });


            var dispatcher = new SimpleEventDispatcher();
            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence(persistedEvents);
            var eventStore = new EventStore(dispatcher, publisher, persistence, loggerFactory.CreateLogger<EventStore>(), useBufferBlock);

            var retrievedEvents = await eventStore.GetEvents(aggregateId);

            Assert.True(retrievedEvents.Any());
            Assert.Equal(events.Length, retrievedEvents.Count());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task EventStore_get_events_returns_empty_enumerable_for_unknown_id(bool useBufferBlock)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var dispatcher = new SimpleEventDispatcher();
            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence();
            var eventStore = new EventStore(dispatcher, publisher, persistence, loggerFactory.CreateLogger<EventStore>(), useBufferBlock);

            var retrievedEvents = await eventStore.GetEvents(Guid.NewGuid());

            Assert.False(retrievedEvents.Any());
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task EventStore_get_version_returns_correct_value(bool useBufferBlock)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid();
            var events = new[] { new Event1(correlationId, 1, aggregateId), new Event1(correlationId, 2, aggregateId) };
            var persistedEvents = events.Select(e => new PersistedEventImpl(e) { State = PersistedEventImpl.EventState.Published }).ToList();
            persistedEvents.Add(new PersistedEventImpl(new Event1(correlationId, 3, aggregateId)) { State = PersistedEventImpl.EventState.Idle });


            var dispatcher = new SimpleEventDispatcher();
            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence(persistedEvents);
            var eventStore = new EventStore(dispatcher, publisher, persistence, loggerFactory.CreateLogger<EventStore>(), useBufferBlock);
            var version = await eventStore.GetVersion(aggregateId);
            eventStore.Dispose();
            Assert.Equal(2, version);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task EventStore_get_version_returns_default_for_unknown_id(bool useBufferBlock)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var dispatcher = new SimpleEventDispatcher();
            var publisher = new SimpleEventPublisher();
            var persistence = new EventPersistence();
            var eventStore = new EventStore(dispatcher, publisher, persistence, loggerFactory.CreateLogger<EventStore>(), useBufferBlock);

            var version = await eventStore.GetVersion(Guid.NewGuid());

            Assert.Equal(AggregateRoot.InitialVersion, version);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task EventStore_dispatches_and_publishes_correct_events_after_start(bool useBufferBlock)
        {
            var loggerFactory = new LoggerFactory();
            loggerFactory.AddProvider(new XunitLoggerProvider(_testOutputHelper));

            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid();

            var dispatcher = new SimpleEventDispatcher();
            var publisher = new SimpleEventPublisher();
            var persistedEvents = new[]
            {
                new PersistedEventImpl(new Event1(correlationId, 1, aggregateId)) { State = PersistedEventImpl.EventState.Published },
                new PersistedEventImpl(new Event1(correlationId, 2, aggregateId)) { State = PersistedEventImpl.EventState.Dispatched },
                new PersistedEventImpl(new Event1(correlationId, 3, aggregateId)) { State = PersistedEventImpl.EventState.Saved }
            };
            var persistence = new EventPersistence(persistedEvents);

            var eventStore = new EventStore(dispatcher, publisher, persistence, loggerFactory.CreateLogger<EventStore>(), useBufferBlock);
            await Task.Delay(150);
            eventStore.Dispose();

            Assert.Equal(1, dispatcher.DispatchedEvents.Count);
            Assert.Equal(2, publisher.PublishedEvents.Count);
            Assert.True(persistence.PersistedEvents.All(e => e.State == PersistedEventImpl.EventState.Published));
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

        private class SimpleEventDispatcher : IEventDispatcher
        {
            public IList<IEvent> DispatchedEvents { get; } = new List<IEvent>();

            public Task Dispatch(IEvent @event, Func<CancellationToken, Task> next, CancellationToken token = default(CancellationToken))
            {
                if (@event is EventFailingToDispatch)
                    throw new InvalidOperationException();

                DispatchedEvents.Add(@event);
                return next(token);
            }
        }

        private class SimpleEventPublisher : IEventPublisher
        {
            public IList<IEvent> PublishedEvents { get; } = new List<IEvent>();

            public Task Publish(IEvent @event, Func<CancellationToken, Task> next, CancellationToken token = default(CancellationToken))
            {
                if (@event is EventFailingToPublish)
                    throw new InvalidOperationException();

                PublishedEvents.Add(@event);
                return next(token);
            }
        }

        private class EventPersistence : IEventPersistence
        {
            private readonly IList<Event> _events;
            private readonly IList<PersistedEventImpl> _persistedEvents;

            public EventPersistence()
            {
                _events = new List<Event>();
                _persistedEvents = new List<PersistedEventImpl>();
            }

            public EventPersistence(IEnumerable<Event> events)
            {
                _events = new List<Event>(events);
                _persistedEvents = new List<PersistedEventImpl>(events.Select(e => new PersistedEventImpl(e) { State = PersistedEventImpl.EventState.Published }));
            }

            public EventPersistence(IEnumerable<PersistedEventImpl> events)
            {
                _events = new List<Event>(events.Select(e => e.Event));
                _persistedEvents = new List<PersistedEventImpl>(events);
            }

            public PersistedEventImpl[] PersistedEvents { get { return _persistedEvents.ToArray(); } }

            public Task<Event[]> Get(Guid aggregateId, int fromVersion = 0, CancellationToken token = default(CancellationToken))
            {
                return _persistedEvents.Where(e => e.Event.AggregateId == aggregateId && e.Event.Version >= fromVersion && CheckVersion(e))
                                       .Select(e => e.Event)
                                       .ToArray()
                                       .AsTask();
            }

            public async Task<Event> GetLastOrDefault(Guid aggregateId, CancellationToken token = default(CancellationToken))
            {
                return (await Get(aggregateId)).LastOrDefault();
            }

            public Task<IPersistedEvent[]> GetUndispatched(CancellationToken token = default(CancellationToken))
            {
                return _persistedEvents.Where(e => e.State == PersistedEventImpl.EventState.Saved)
                                       .Cast<IPersistedEvent>()
                                       .ToArray()
                                       .AsTask();
            }

            public Task<IPersistedEvent[]> GetUnpublished(CancellationToken token = default(CancellationToken))
            {
                return _persistedEvents.Where(e => e.State == PersistedEventImpl.EventState.Dispatched)
                                       .Cast<IPersistedEvent>()
                                       .ToArray()
                                       .AsTask();
            }

            public Task<IPersistedEvent[]> Save(IEnumerable<Event> events, CancellationToken token = default(CancellationToken))
            {
                var result = new List<IPersistedEvent>();
                foreach (var @event in events)
                {
                    _events.Add(@event);
                    var persistedEvent = new PersistedEventImpl(@event)
                    {
                        State = PersistedEventImpl.EventState.Saved
                    };
                    _persistedEvents.Add(persistedEvent);
                    result.Add(persistedEvent);
                }
                return result.ToArray().AsTask();
            }

            public Task SetDispatched(IPersistedEvent persistedEvent, CancellationToken token = default(CancellationToken))
            {
                var persistedEvents = new IPersistedEvent[] { persistedEvent };
                persistedEvents.OfType<PersistedEventImpl>().ForEach(pe =>
                {
                    var @event = _persistedEvents.FirstOrDefault(e => e.EventId == pe.EventId);
                    if (@event != null)
                        @event.State = PersistedEventImpl.EventState.Dispatched;
                });

                return Task.CompletedTask;
            }

            public Task SetPublished(IPersistedEvent persistedEvent, CancellationToken token = default(CancellationToken))
            {
                var persistedEvents = new IPersistedEvent[] { persistedEvent };
                persistedEvents.OfType<PersistedEventImpl>().ForEach(pe =>
                {
                    var @event = _persistedEvents.FirstOrDefault(e => e.EventId == pe.EventId);
                    if (@event != null)
                        @event.State = PersistedEventImpl.EventState.Published;
                });

                return Task.CompletedTask;
            }

            private static bool CheckVersion(PersistedEventImpl persistedEvent)
            {
                return persistedEvent.State != PersistedEventImpl.EventState.Idle;
            }
        }

        private class PersistedEventImpl : IPersistedEvent
        {
            public enum EventState { Idle, Saved, Dispatched, Published }

            public PersistedEventImpl(Event @event)
            {
                State = EventState.Idle;
                EventId = Guid.NewGuid();
                Event = @event;
            }

            public Guid EventId { get; }

            public EventState State { get; set; }

            public Event Event { get; set; }
        }
    }
}