using System;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Internal
{
    internal class AggregateSnapshotRespositoryDecorator : IAggregateRepository
    {
        private readonly IAggregateFactory _aggregateFactory;
        private readonly IAggregateRepository _decoratedRepository;
        private readonly IEventStore _eventStore;
        private readonly ISnapshotStore _snapshotStore;
        private readonly ISnapshotStrategy _snapshotStrategy;

        public AggregateSnapshotRespositoryDecorator(IAggregateRepository decorated, IAggregateFactory aggregateFactory, IEventStore eventStore, ISnapshotStore snapshotStore, ISnapshotStrategy snapshotStrategy)
        {
            _decoratedRepository = decorated ?? throw new ArgumentNullException(nameof(decorated));
            _aggregateFactory = aggregateFactory ?? throw new ArgumentNullException(nameof(aggregateFactory));
            _eventStore = eventStore ?? throw new ArgumentNullException(nameof(eventStore));
            _snapshotStore = snapshotStore ?? throw new ArgumentNullException(nameof(snapshotStore));
            _snapshotStrategy = snapshotStrategy ?? throw new ArgumentNullException(nameof(snapshotStrategy));
        }

        public Task<T> Get<T>(string aggregateId, CancellationToken cancellationToken = default) where T : AggregateRoot
            => !_snapshotStrategy.IsSnapshottable(typeof(T))
                ? _decoratedRepository.Get<T>(aggregateId, cancellationToken)
                : LoadFromSnapshot(aggregateId, _aggregateFactory.CreateAggregate<T>(), cancellationToken);

        public Task Save<T>(T aggregate, int expectedVersion, CancellationToken cancellationToken = default) where T : AggregateRoot
            => DoSaveAsync(aggregate, () => _decoratedRepository.Save(aggregate, expectedVersion, cancellationToken), cancellationToken);

        private async Task DoSaveAsync<T>(T aggregate, Func<Task> saveFunc, CancellationToken cancellationToken) where T : AggregateRoot
        {
            var pendingChangesCount = aggregate.PendingChangesCount;
            await saveFunc();
            if (_snapshotStrategy.TakeSnapshot(aggregate, pendingChangesCount))
            {
                var snapshot = await ((ISnapshottable)aggregate).CreateSnapshot(cancellationToken);
                await _snapshotStore.Save(snapshot, cancellationToken);
            }
        }

        private async Task<T> LoadFromSnapshot<T>(string aggregateId, T aggregate, CancellationToken cancellationToken) where T : AggregateRoot
        {
            var snapshot = await _snapshotStore.Get(aggregateId, cancellationToken);
            if (snapshot != null && aggregate is ISnapshottable snapshottable)
            {
                await snapshottable.RestoreSnapshot(snapshot, cancellationToken);
                var eventStream = await _eventStore.GetEventStream(aggregateId, snapshot.Version + 1, cancellationToken);
                aggregate.Load(eventStream);
                return aggregate;
            }

            return await _decoratedRepository.Get<T>(aggregateId, cancellationToken);
        }
    }
}