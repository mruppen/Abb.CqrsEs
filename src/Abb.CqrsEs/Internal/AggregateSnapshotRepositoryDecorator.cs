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

        public Task<T> Get<T>(string aggregateId, CancellationToken token = default) where T : AggregateRoot
            => !_snapshotStrategy.IsSnapshottable(typeof(T))
                ? _decoratedRepository.Get<T>(aggregateId, token)
                : LoadFromSnapshot(aggregateId, _aggregateFactory.CreateAggregate<T>(), token);

        public Task Save<T>(T aggregate, int expectedVersion, CancellationToken token = default) where T : AggregateRoot
            => DoSaveAsync(aggregate, () => _decoratedRepository.Save(aggregate, expectedVersion, token), token);

        private async Task DoSaveAsync<T>(T aggregate, Func<Task> saveFunc, CancellationToken token) where T : AggregateRoot
        {
            var pendingChangesCount = aggregate.PendingChangesCount;
            await saveFunc();
            if (_snapshotStrategy.TakeSnapshot(aggregate, pendingChangesCount))
            {
                var snapshot = await ((ISnapshottable)aggregate).CreateSnapshot(token);
                await _snapshotStore.Save(snapshot, token);
            }
        }

        private async Task<T> LoadFromSnapshot<T>(string aggregateId, T aggregate, CancellationToken token) where T : AggregateRoot
        {
            var snapshot = await _snapshotStore.Get(aggregateId, token);
            if (snapshot != null && aggregate is ISnapshottable snapshottable)
            {
                await snapshottable.RestoreSnapshot(snapshot, token);
                var eventStream = await _eventStore.GetEventStream(aggregateId, snapshot.Version + 1, token);
                aggregate.Load(eventStream);
                return aggregate;
            }

            return await _decoratedRepository.Get<T>(aggregateId, token);
        }
    }
}