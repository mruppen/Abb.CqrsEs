using System;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Internal
{
    public class AggregateSnapshotInteractionService : IAggregateInteractionService
    {
        private readonly IAggregateInteractionService _decoratedRepository;
        private readonly IAggregateFactory _aggregateFactory;
        private readonly IEventPersistence _eventPersistence;
        private readonly IEventStore _eventStore;
        private readonly ISnapshotStore _snapshotStore;
        private readonly ISnapshotStrategy _snapshotStrategy;

        public AggregateSnapshotInteractionService(IAggregateInteractionService decorated, IAggregateFactory aggregateFactory, IEventPersistence eventPersistence, IEventStore eventStore, ISnapshotStore snapshotStore, ISnapshotStrategy snapshotStrategy)
        {
            _decoratedRepository = decorated ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(decorated));
            _aggregateFactory = aggregateFactory ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(aggregateFactory));
            _eventPersistence = eventPersistence;
            _eventStore = eventStore ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(eventStore));
            _snapshotStore = snapshotStore ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(snapshotStore));
            _snapshotStrategy = snapshotStrategy ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(snapshotStrategy));
        }

        public Task<T> Get<T>(Guid aggregateId, CancellationToken token = default) where T : AggregateRoot
        {
            if (!_snapshotStrategy.IsSnapshottable(typeof(T)))
                return _decoratedRepository.Get<T>(aggregateId, token);
            return LoadFromSnapshot(aggregateId, _aggregateFactory.CreateAggregate<T>(), token);
        }

        public Task Save<T>(T aggregate, CancellationToken token = default) where T : AggregateRoot
        {
            return DoSaveAsync(aggregate, () => _decoratedRepository.Save(aggregate, token), token);
        }

        public Task Save<T>(T aggregate, int expectedVersion, CancellationToken token = default) where T : AggregateRoot
        {
            return DoSaveAsync(aggregate, () => _decoratedRepository.Save(aggregate, expectedVersion, token), token);
        }

        private Task DoSaveAsync<T>(T aggregate, Func<Task> saveFunc, CancellationToken token) where T : AggregateRoot
        {
            var saveTask = saveFunc();
            if (!_snapshotStrategy.IsSnapshottable(typeof(T)))
                return saveTask;
            else
                return saveTask.Then(() => ((ISnapshottable)aggregate).CreateSnapshot(token))
                               .Then(snapshot => _snapshotStore.Save(snapshot, token));
        }

        private async Task<T> LoadFromSnapshot<T>(Guid aggregateId, T aggregate, CancellationToken token) where T : AggregateRoot
        {
            var snapshot = await _snapshotStore.Get(aggregateId, token);
            if (snapshot == null)
                return await _decoratedRepository.Get<T>(aggregateId, token);

            await (aggregate as ISnapshottable).RestoreSnapshot(snapshot, token);
            var events = await _eventStore.GetEvents(aggregateId, snapshot.Version + 1, _eventPersistence, token);
            await aggregate.LoadFromHistory(events, token);
            return aggregate;
        }
    }
}
