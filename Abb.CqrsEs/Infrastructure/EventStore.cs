using Microsoft.Extensions.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Infrastructure
{
    public class EventStore : IDisposable, IEventStore
    {
        private readonly IEventPublisher _publisher;
        private readonly IEventPersistence _persistence;
        private readonly IEventCache _eventCache;
        private readonly ILogger _logger;
        private readonly ConcurrentDictionary<Guid, AggregateEventStream> _emittingEvents = new ConcurrentDictionary<Guid, AggregateEventStream>();
        private bool _disposed = false;

        public EventStore(IEventPublisher eventPublisher, IEventPersistence eventPersistence, IEventCache eventCache, ILogger<EventStore> logger)
        {
            _publisher = eventPublisher ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(eventPublisher));
            _persistence = eventPersistence ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(eventPersistence));
            _eventCache = eventCache ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(eventCache));
            _logger = logger ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(logger));

            InsertPendingEvents().GetAwaiter().GetResult();
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

        public Task SaveAndPublish(Guid aggregateId, IEnumerable<Event> events, Func<CancellationToken, Task> commitChanges = null, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (events == null) throw new ArgumentNullException(nameof(events));
            _logger.Info(() =>
            {
                return $"Saving and publishing events for aggegrate with id {aggregateId}";
            });

            var eventsArray = events.ToArray();
            try
            {
                return _persistence.Save(events, cancellationToken)
                    .Then(async () =>
                    {
                        if (commitChanges != null)
                            await commitChanges(cancellationToken);
                    })
                    .Then(() =>
                    {
                        return Publish(aggregateId, eventsArray, cancellationToken);
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
                _disposed = true;
            }
        }

        private async Task InsertPendingEvents()
        {
            var pendingEvents = await _eventCache.GetAll();
            foreach (var events in pendingEvents)
            {
                await Publish(events.Key, events.ToArray(), CancellationToken.None);
            }
        }

        private async Task Publish(Guid aggregateId, Event[] events, CancellationToken cancellationToken)
        {

            var aes = _emittingEvents.GetOrAdd(aggregateId,
                _ => new AggregateEventStream(
                    aggregateId,
                    (@event, token) => _publisher.Publish(@event, token),
                    @event => _eventCache.Delete(@event)));

            await events.ForEachAsync(async @event => await _eventCache.Add(@event));
            await aes.AddEventStream(events, cancellationToken);
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

        private class AggregateEventStream
        {
            private readonly Func<Event, CancellationToken, Task> _publish;
            private readonly Func<Event, Task> _complete;
            private readonly AsyncLock _asyncLock = new AsyncLock();
            private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
            private Task _processingTask = Task.CompletedTask;

            public AggregateEventStream(Guid aggregateId, Func<Event, CancellationToken, Task> publish, Func<Event, Task> complete)
            {
                AggregateId = aggregateId;
                _publish = publish;
                _complete = complete;
            }

            public Guid AggregateId { get; }

            public async Task AddEventStream(Event[] events, CancellationToken cancellationToken)
            {
                using (await _asyncLock.Lock(cancellationToken))
                {
                    if (_processingTask.IsCanceled)
                        throw new TaskCanceledException();

                    if (_processingTask.IsFaulted)
                        throw _processingTask.Exception?.Flatten() as Exception ?? new InvalidOperationException();

                    if (_processingTask.IsCompleted)
                    {
                        _processingTask = Task.Run(async () => await PublishEventStream(events, cancellationToken));
                    }
                    else
                    {
                        _processingTask = _processingTask.ContinueWith(async t =>
                        {
                            t.ThrowIfFaultedOrCanceled();
                            await PublishEventStream(events, cancellationToken);
                        });
                    }
                }
            }

            private async Task PublishEventStream(Event[] events, CancellationToken cancellationToken)
            {
                if (_tokenSource.IsCancellationRequested)
                    throw new TaskCanceledException();

                var registration = cancellationToken.Register(() => _tokenSource.Cancel());
                try
                {
                    foreach (var @event in events)
                    {
                        await _publish(@event, _tokenSource.Token);
                        await _complete(@event);
                    }
                }
                finally
                {
                    registration.Dispose();
                }
            }
        }
    }
}
