using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.EventStore
{
    public class EventStore : IDisposable, IEventStore
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
            GC.SuppressFinalize(this);
        }

        public Task<EventStream> GetEventStream(string aggregateId, int fromVersion, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            CheckAggregateIdOrThrow(aggregateId);

            async Task<EventStream> DoGetEventStream()
            {
                _logger.Info(() => $"Getting events of aggregate with id {aggregateId} starting with version {fromVersion}.");
                try
                {
                    var events = await _eventPersistence.Get(aggregateId, fromVersion, cancellationToken).ToArrayAsync().ConfigureAwait(false)
                        ?? new Event[0];
                    _logger.Debug(() => $"Retrieved {events.Length} events for aggregate {aggregateId}.");
                    return new EventStream(aggregateId, events);
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

            return DoGetEventStream();
        }

        public Task<EventStream> GetEventStream(string aggregateId, CancellationToken cancellationToken = default)
            => GetEventStream(aggregateId, AggregateRoot.InitialVersion, cancellationToken);

        public Task<int> GetVersion(string aggregateId, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            CheckAggregateIdOrThrow(aggregateId);

            async Task<int> DoGetVersion()
            {
                _logger.Debug(() => $"Getting version of aggregate with id {aggregateId}");
                try
                {
                    var lastEvent = await _eventPersistence.GetLastOrDefault(aggregateId, cancellationToken).ConfigureAwait(false);
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

            return DoGetVersion();
        }

        public Task SaveAndPublish(EventStream eventStream, Action? commit, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (eventStream == null)
            {
                throw new ArgumentNullException(nameof(eventStream));
            }

            async Task DoSaveAndPublish()
            {
                _logger.Info(() => $"Saving and publishing events for aggegrate with id {eventStream.AggregateId}");

                try
                {
                    await _eventPersistence.Save(eventStream.AggregateId, eventStream.Events, cancellationToken);
                    commit?.Invoke();
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

                try
                {
                    await _eventPublisher.Publish(eventStream, cancellationToken);
                }
                catch (Exception e)
                {
                    _logger.Error(() => $"Could not publish an event stream for aggregate '{eventStream.AggregateId}. This EventStore is not built to handle such cases.", e);
                }
            }

            return DoSaveAndPublish();
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                _disposed = true;
            }
        }

        private static void CheckAggregateIdOrThrow(string aggregateId)
        {
            if (string.IsNullOrEmpty(aggregateId))
            {
                throw new ArgumentNullException(nameof(aggregateId));
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