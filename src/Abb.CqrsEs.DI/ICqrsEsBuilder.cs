using Abb.CqrsEs;
using System;

namespace Microsoft.Extensions.DependencyInjection
{
    public interface ICqrsEsBuilder
    {
        ICqrsEsOptionalBuilder Optional { get; }

        ICqrsEsBuilder AddEventPublisher<T>() where T : IEventPublisher;

        ICqrsEsBuilder AddEventPublisher(Type type);

        ICqrsEsBuilder AddEventCache<T>() where T : IEventCache;

        ICqrsEsBuilder AddEventCache(Type type);

        ICqrsEsBuilder AddEventPersistence<T>() where T : IEventPersistence;

        ICqrsEsBuilder AddEventPersistence(Type type);
    }

    public interface ICqrsEsOptionalBuilder
    {
        ICqrsEsBuilder RegisterHandlers();

        ICqrsEsBuilder OverrideEventConverter<T>() where T : IEventConverter;

        ICqrsEsBuilder OverrideEventConverter(Type type);

        ICqrsEsBuilder EnableSnapshots<TStore>() where TStore : ISnapshotStore;

        ICqrsEsBuilder EnableSnapshots<TStore, TStrategy>() where TStore : ISnapshotStore where TStrategy : ISnapshotStrategy;

        ICqrsEsBuilder EnableSnapshots(Type snapshotStoreType);

        ICqrsEsBuilder EnableSnapshots(Type snapshotStoreType, Type snapshotStrategyType);
    }
}
