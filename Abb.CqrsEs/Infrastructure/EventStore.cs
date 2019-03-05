using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Abb.CqrsEs.Infrastructure
{
    public class EventStore : IDisposable, IEventStore
    {
        private readonly BufferBlock<PublishingEventStream> _publisherQueue = new BufferBlock<PublishingEventStream>();

        private event EventHandler<PublishingEventStream> OnSaved;

        private readonly bool _useBufferBlock;

        private readonly IEventDispatcher _dispatcher;
        private readonly IEventPublisher _publisher;
        private readonly IEventPersistence _persistence;
        private readonly ILogger _logger;
        private readonly IDisposable _subscriber;
        private bool _disposed = false;

        public EventStore(IEventDispatcher eventDispatcher, IEventPublisher eventPublisher, IEventPersistence eventPersistence, ILogger<EventStore> logger, bool useBufferBlock = true)
        {
            _dispatcher = eventDispatcher ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(eventDispatcher));
            _publisher = eventPublisher ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(eventPublisher));
            _persistence = eventPersistence ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(eventPersistence));
            _logger = logger ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(logger));
            _useBufferBlock = useBufferBlock;

            var observable = GetObservable();
            _subscriber = GetSubscription(observable);

            InsertInitialEventStreams();
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public Task<IEnumerable<Event>> GetEvents(Guid aggregateId, CancellationToken token = default)
            => GetEvents(aggregateId, AggregateRoot.InitialVersion, token);

        public Task<IEnumerable<Event>> GetEvents(Guid aggregateId, int fromVersion, CancellationToken token = default)
        {
            ThrowIfDisposed();
            CheckAggregateIdOrThrow(aggregateId);
            _logger.Info(() => $"Getting events of aggregate with id {aggregateId} starting with version {fromVersion}.");
            try
            {
                return _persistence.Get(aggregateId, fromVersion, token)
                    .Then(events =>
                    {
                        var ensured = events ?? new Event[0];
                        _logger.Debug(() => $"Retrieved {ensured.Length} events for aggregate {aggregateId}.");
                        return ensured.AsEnumerable().AsTask();
                    });
            }
            catch (ArgumentException) { throw; }
            catch (UnknownGuidException) { throw; }
            catch (InvalidOperationException) { throw; }
            catch (Exception e)
            {
                throw new InvalidOperationException("Operation could not be completed.", e);
            }
        }

        public async Task<int> GetVersion(Guid aggregateId, CancellationToken token = default)
        {
            ThrowIfDisposed();
            CheckAggregateIdOrThrow(aggregateId);
            _logger.Debug(() => $"Getting version of aggregate with id {aggregateId}");
            try
            {
                var lastEvent = await _persistence.GetLastOrDefault(aggregateId, token).ConfigureAwait(false);
                if (lastEvent == null)
                {
                    _logger.Debug(() => $"Aggregate {aggregateId} does not exist and version is {AggregateRoot.InitialVersion}.");
                    return AggregateRoot.InitialVersion;
                }

                _logger.Debug(() => $"Aggregate with id {aggregateId} has version {lastEvent.Version}.");
                return lastEvent.Version;
            }
            catch (ArgumentException) { throw; }
            catch (UnknownGuidException) { throw; }
            catch (InvalidOperationException) { throw; }
            catch (Exception e)
            {
                throw new InvalidOperationException("Operation could not be completed.", e);
            }
        }

        public Task SaveAndPublish(IEnumerable<Event> events, Func<CancellationToken, Task> commitChanges = null, CancellationToken token = default)
        {
            ThrowIfDisposed();
            if (events == null) throw new ArgumentNullException(nameof(events));
            _logger.Info(() =>
            {
                var firstEvent = events.FirstOrDefault();
                return $"Saving and publishing events for aggegrate with id {firstEvent?.AggregateId}";
            });

            try
            {
                return _persistence.Save(events, token)
                    .Then(async savedEvents =>
                    {
                        if (commitChanges != null)
                            await commitChanges(token);
                        return savedEvents;
                    })
                    .Then(savedEvents =>
                    {
                        return EmitEventStream(new PublishingEventStream { Events = savedEvents }, token);
                    });
            }
            catch (InvalidOperationException) { throw; }
            catch (AggregateException e)
            {
                throw new InvalidOperationException("Operation could not be completed.", e.InnerException);
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Operation could not be completed.", e);
            }
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                _subscriber?.Dispose();
                _disposed = true;
            }
        }

        private IObservable<PublishingEventStream> GetObservable()
        {
            IObservable<PublishingEventStream> observable;
            if (_useBufferBlock)
            {
                observable = Observable.Create<PublishingEventStream>(async (observer, token) =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        try
                        {
                            var nextItem = await _publisherQueue.ReceiveAsync(token);
                            observer.OnNext(nextItem);
                        }
                        catch (TaskCanceledException)
                        {
                            _logger.Info(() => "EventStore has been disposed.");
                        }
                        catch (Exception e)
                        {
                            _logger.Warning(() => $"Processing queue item failed.", e);
                        }
                    }

                    observer.OnCompleted();
                    return Disposable.Empty;
                });
            }
            else
            {
                observable = Observable.FromEventPattern<PublishingEventStream>(h => OnSaved += h, h => OnSaved -= h)
                                       .Select(e => e.EventArgs);
            }

            return observable.Publish()
                             .RefCount();
        }

        private IDisposable GetSubscription(IObservable<PublishingEventStream> observable)
        {
            var observer = new Observer(_persistence, _dispatcher, _publisher, _logger);
            if (_useBufferBlock)
            {
                return observable.ObserveOn(TaskPoolScheduler.Default).Subscribe(observer);
            }
            else
            {
                return observable.ObserveOn(new EventLoopScheduler()).Subscribe(observer);
            }
        }

        private async Task EmitEventStream(PublishingEventStream eventStream, CancellationToken token)
        {
            if (_useBufferBlock)
            {
                await _publisherQueue.SendAsync(eventStream, token);
            }
            else
            {
                token.ThrowIfCancellationRequested();
                OnSaved(this, eventStream);
            }
        }

        private void InsertInitialEventStreams()
        {
            var unpublishedEvents = _persistence.GetUnpublished().GetAwaiter().GetResult();
            EmitEventStream(new PublishingEventStream { Events = unpublishedEvents, PublishOnly = true, RetryCount = 0 }, CancellationToken.None)
                .GetAwaiter().GetResult();

            var undispatchedEvents = _persistence.GetUndispatched().GetAwaiter().GetResult();
            EmitEventStream(new PublishingEventStream { Events = undispatchedEvents, PublishOnly = false, RetryCount = 0 }, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(EventStore));
        }

        private static void CheckAggregateIdOrThrow(Guid aggregateId)
        {
            if (aggregateId == default || aggregateId == Guid.Empty)
                throw ExceptionHelper.ArgumentMustNotBeNullOrDefault(nameof(aggregateId));
        }

        private class Observer : IObserver<PublishingEventStream>
        {
            private readonly IEventPersistence _persistence;
            private readonly IEventDispatcher _dispatcher;
            private readonly IEventPublisher _publisher;
            private readonly ILogger _logger;
            private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();

            public Observer(IEventPersistence persistence, IEventDispatcher dispatcher, IEventPublisher publisher, ILogger logger)
            {
                _persistence = persistence;
                _dispatcher = dispatcher;
                _publisher = publisher;
                _logger = logger;
            }

            public void OnCompleted()
            {
                _tokenSource.Cancel();
            }

            public void OnError(Exception error)
            { }

            public void OnNext(PublishingEventStream value)
            {
                IPersistedEvent[] dispatchedEvents;
                _logger.Debug(() => $"Processing next {nameof(PublishingEventStream)} instance.");
                if (value.PublishOnly)
                {
                    dispatchedEvents = value.Events;
                }
                else
                {
                    try
                    {
                        dispatchedEvents = Dispatch(value.Events, _tokenSource.Token);
                    }
                    catch (Exception e)
                    {
                        _logger.Warning(() => $"Dispatching of events failed.", e);
                        return;
                    }
                }

                try
                {
                    var publishedEvents = Publish(dispatchedEvents, _tokenSource.Token);
                }
                catch (Exception e)
                {
                    _logger.Warning(() => $"Publishing of events failed.", e);
                    return;
                }
            }

            private IPersistedEvent[] Dispatch(IPersistedEvent[] persistedEvents, CancellationToken token)
            {
                persistedEvents?.ForEach(e =>
                {
                    _logger.Debug(() => $"Dispatch event with type {e.Event.GetType().Name} and version {e.Event.Version} for aggregate {e.Event.AggregateId}.");
                    _dispatcher.Dispatch(e.Event, cancellationToken => _persistence.SetDispatched(e, cancellationToken), token).GetAwaiter().GetResult();
                });
                return persistedEvents;
            }

            private IPersistedEvent[] Publish(IPersistedEvent[] persistedEvents, CancellationToken token)
            {
                persistedEvents?.ForEach(e =>
                {
                    _logger.Debug(() => $"Publish event with type {e.Event.GetType().Name} and version {e.Event.Version} for aggregate {e.Event.AggregateId}.");
                    _publisher.Publish(e.Event, cancellationToken => _persistence.SetPublished(e, cancellationToken), token).GetAwaiter().GetResult();
                });
                return persistedEvents;
            }
        }

        private struct PublishingEventStream
        {
            public int RetryCount { get; set; }

            public IPersistedEvent[] Events { get; set; }

            public bool PublishOnly { get; set; }
        }
    }
}
