using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs
{
    public class AggregateRepository : IAggregateRepository
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

        public async Task<T> Get<T>(string aggregateId, CancellationToken cancellationToken = default) where T : AggregateRoot
        {
            _logger.Debug(() => $"Initializing aggregate of type {typeof(T).Name} with id {aggregateId}");
            var aggregate = _aggregateFactory.CreateAggregate<T>();
            aggregate.Id = aggregateId;
            var eventStream = await _eventStore.GetEventStream(aggregateId, AggregateRoot.InitialVersion + 1, cancellationToken);
            aggregate.Load(eventStream.Events);
            return aggregate;
        }

        public Task Save<T>(T aggregate, int expectedVersion = int.MinValue, CancellationToken cancellationToken = default) where T : AggregateRoot
        {
            if (aggregate == null)
            {
                throw new ArgumentNullException(nameof(aggregate));
            }

            async Task DoSave()
            {
                _logger.Debug(() => $"Save events of aggregate {aggregate.AggregateIdentifier}");
                var actualVersion = await _eventStore.GetVersion(aggregate.Id, cancellationToken);
                if (expectedVersion != int.MinValue && actualVersion != expectedVersion)
                {
                    _logger.Warning(() => $"ExpectedVersion {expectedVersion} does not match actual version {actualVersion} of aggregate  {aggregate.AggregateIdentifier}");
                    throw new ConcurrencyException($"Expected version and actual version of aggregate {aggregate.AggregateIdentifier} do not match.");
                }
                try
                {
                    var events = aggregate.GetPendingChanges();
                    _logger.Debug(() => $"Aggregate {aggregate.AggregateIdentifier} has {events.Count()} pending changes.");
                    if (!events.Any())
                    {
                        aggregate.CommitChanges();
                        return;
                    }

                    await _eventStore.SaveAndPublish(new EventStream(aggregate.Id, actualVersion, events.ToArray()), aggregate.CommitChanges, cancellationToken).ConfigureAwait(false);
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

            return DoSave();
        }
    }
}