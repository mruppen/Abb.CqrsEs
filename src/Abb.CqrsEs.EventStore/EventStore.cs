using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.EventStore
{
    internal class EventStore : IDisposable, IEventStore
    {
        private readonly IEventPersistence _eventPersistence;
        private readonly IEventPublisher _eventPublisher;
        private readonly ILogger _logger;
        private bool _disposed = false;

        public EventStore(IEventPersistence eventPersistence, IEventPublisher eventPublisher, ILogger<EventStore> logger)
        {
            _eventPersistence = eventPersistence;
            _eventPublisher = eventPublisher;
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public Task<IEnumerable<Event>> GetEventStream(string aggregateId, int fromVersion, CancellationToken token = default)
        {
            ThrowIfDisposed();
            CheckAggregateIdOrThrow(aggregateId);
            _logger.Info(() => $"Getting events of aggregate with id {aggregateId} starting with version {fromVersion}.");
            try
            {
                return _eventPersistence
                    .Get(aggregateId, fromVersion, token)
                    .Then(events =>
                    {
                        var ensured = events ?? new Event[0];
                        _logger.Debug(() => $"Retrieved {ensured.Length} events for aggregate {aggregateId}.");
                        return ensured.AsEnumerable().AsTask();
                    });
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (UnknownAggregateIdException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Operation could not be completed.", e);
            }
        }

        public Task<IEnumerable<Event>> GetEventStream(string aggregateId, CancellationToken token = default)
                    => GetEventStream(aggregateId, AggregateRoot.InitialVersion, token);

        public async Task<int> GetVersion(string aggregateId, CancellationToken token = default)
        {
            ThrowIfDisposed();
            CheckAggregateIdOrThrow(aggregateId);
            _logger.Debug(() => $"Getting version of aggregate with id {aggregateId}");
            try
            {
                var lastEvent = await _eventPersistence.GetLastOrDefault(aggregateId, token).ConfigureAwait(false);
                if (lastEvent == null)
                {
                    _logger.Debug(() => $"Aggregate {aggregateId} does not exist and version is {AggregateRoot.InitialVersion}.");
                    return AggregateRoot.InitialVersion;
                }

                _logger.Debug(() => $"Aggregate with id {aggregateId} has version {lastEvent.Version}.");
                return lastEvent.Version;
            }
            catch (ArgumentException)
            {
                throw;
            }
            catch (UnknownAggregateIdException)
            {
                throw;
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException("Operation could not be completed.", e);
            }
        }

        public Task SaveAndPublish(EventStream eventStream, Func<CancellationToken, Task> commitChanges, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (eventStream == null)
            {
                throw new ArgumentNullException(nameof(eventStream));
            }

            _logger.Info(() => $"Saving and publishing events for aggegrate with id {eventStream.AggregateId}");

            try
            {
                return _eventPersistence
                    .Save(eventStream, cancellationToken)
                    .Then(() => commitChanges?.Invoke(cancellationToken) ?? Task.CompletedTask)
                    .Then(() => _eventPublisher.Publish(eventStream, cancellationToken));
            }
            catch (InvalidOperationException)
            {
                throw;
            }
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

        private static void CheckAggregateIdOrThrow(string aggregateId)
        {
            if (string.IsNullOrEmpty(aggregateId))
            {
                throw new ArgumentNullExceptionOrDefault(nameof(aggregateId));
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(EventStore));
            }
        }
    }
}