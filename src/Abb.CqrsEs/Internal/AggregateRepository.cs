using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Internal
{
    public class AggregateRepository : IAggregateRepository
    {
        private readonly IEventStore _eventStore;
        private readonly IAggregateFactory _aggregateFactory;
        private readonly IEventPersistence _eventPersistence;
        private readonly ILogger<AggregateRepository> _logger;

        public AggregateRepository(IEventStore eventStore, IAggregateFactory aggregateFactory, IEventPersistence eventPersistence, ILogger<AggregateRepository> logger)
        {
            _eventStore = eventStore ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(eventStore));
            _aggregateFactory = aggregateFactory ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(aggregateFactory));
            _eventPersistence = eventPersistence;
            _logger = logger ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(logger));
        }

        public async Task<T> Get<T>(Guid aggregateId, CancellationToken token = default) where T : AggregateRoot
        {
            _logger.Debug(() => $"Initializing aggregate of type {typeof(T).Name} with id {aggregateId}");
            var aggregate = _aggregateFactory.CreateAggregate<T>();
            aggregate.Id = aggregateId;
            var events = await _eventStore.GetEvents(aggregateId, _eventPersistence, token);
            _logger.Debug(() => $"Load event history in aggregate {aggregate.AggregateIdentifier}.");
            await aggregate.LoadFromHistory(events, token);
            return aggregate;
        }

        public Task Save<T>(T aggregate, CancellationToken token = default) where T : AggregateRoot
        {
            return Save(aggregate, aggregate.Version - aggregate.PendingChangesCount, token);
        }

        public async Task Save<T>(T aggregate, int expectedVersion, CancellationToken token = default) where T : AggregateRoot
        {
            if (aggregate == null)
            {
                throw ExceptionHelper.ArgumentMustNotBeNull(nameof(aggregate));
            }

            _logger.Debug(() => $"Save events of aggregate {aggregate.AggregateIdentifier}");
            var actualVersion = 0;
            if (expectedVersion <= 0 && (actualVersion = await _eventStore.GetVersion(aggregate.Id, _eventPersistence, token)) != expectedVersion)
            {
                _logger.Warning(() => $"ExpectedVersion {expectedVersion} does not match actual version {actualVersion} of aggregate  {aggregate.AggregateIdentifier}");
                throw new ConcurrencyException($"Expected version and actual version of aggregate {aggregate.AggregateIdentifier} do not match.");
            }
            try
            {
                var eventStream = await aggregate.GetPendingChanges(token);
                _logger.Debug(() => $"Aggregate {aggregate.AggregateIdentifier} has {eventStream.Length} pending changes.");
                if (eventStream.Length == 0)
                {
                    await aggregate.CommitChanges(token);
                    return;
                }
                await _eventStore.SaveAndPublish(aggregate.Id, eventStream, c => aggregate.CommitChanges(c), _eventPersistence, token).ConfigureAwait(false);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new InvalidOperationException($"Exception occured when saving aggregate {aggregate.AggregateIdentifier}", e);
            }
        }
    }
}
