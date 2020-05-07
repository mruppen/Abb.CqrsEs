using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Internal
{
    internal class AggregateRepository : IAggregateRepository
    {
        private readonly IAggregateFactory _aggregateFactory;
        private readonly IEventStore _eventStore;
        private readonly ILogger<AggregateRepository> _logger;

        public AggregateRepository(IEventStore eventStore, IAggregateFactory aggregateFactory, ILogger<AggregateRepository> logger)
        {
            _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            _aggregateFactory = aggregateFactory ?? throw new ArgumentNullException(nameof(aggregateFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<T> Get<T>(string aggregateId, CancellationToken token = default) where T : AggregateRoot
        {
            _logger.Debug(() => $"Initializing aggregate of type {typeof(T).Name} with id {aggregateId}");
            var aggregate = _aggregateFactory.CreateAggregate<T>();
            var eventStream = await _eventStore.GetEventStream(aggregateId, token);
            aggregate.Load(eventStream);
            return aggregate;
        }

        public async Task Save<T>(T aggregate, int expectedVersion, CancellationToken token = default) where T : AggregateRoot
        {
            if (aggregate == null)
            {
                throw new ArgumentNullException(nameof(aggregate));
            }

            _logger.Debug(() => $"Save events of aggregate {aggregate.AggregateIdentifier}");
            var actualVersion = await _eventStore.GetVersion(aggregate.Id, token);
            if (expectedVersion != -1 && actualVersion != expectedVersion)
            {
                _logger.Warning(() => $"ExpectedVersion {expectedVersion} does not match actual version {actualVersion} of aggregate  {aggregate.AggregateIdentifier}");
                throw new ConcurrencyException($"Expected version and actual version of aggregate {aggregate.AggregateIdentifier} do not match.");
            }
            try
            {
                var eventStream = aggregate.GetPendingChanges();
                _logger.Debug(() => $"Aggregate {aggregate.AggregateIdentifier} has {eventStream.Events.Count()} pending changes.");
                if (!eventStream.Events.Any())
                {
                    aggregate.CommitChanges();
                    return;
                }
                await _eventStore.SaveAndPublish(eventStream, aggregate.CommitChanges, token).ConfigureAwait(false);
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