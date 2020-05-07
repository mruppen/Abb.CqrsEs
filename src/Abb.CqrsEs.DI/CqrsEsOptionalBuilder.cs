using Microsoft.Extensions.DependencyInjection;
using System;

namespace Abb.CqrsEs.DI
{
    internal class CqrsEsOptionalBuilder : ICqrsEsOptionalBuilder
    {
        private readonly ICqrsEsBuilder _cqrsEsBuilder;
        private Action _enableHandlerRegistration;
        private Action<Type, Type> _enableSnapshots;
        private Action<Type> _overrideEventConverter;

        public CqrsEsOptionalBuilder(ICqrsEsBuilder cqrsEsBuilder, Action<Type> overrideEventConverter, Action<Type, Type> enableSnapshots, Action enableHandlerRegistration)
        {
            _cqrsEsBuilder = cqrsEsBuilder;
            _overrideEventConverter = overrideEventConverter;
            _enableSnapshots = enableSnapshots;
            _enableHandlerRegistration = enableHandlerRegistration;
        }

        public ICqrsEsBuilder EnableSnapshots<TStore>() where TStore : ISnapshotStore
            => EnableSnapshots(typeof(TStore), typeof(DefaultSnapshotStrategy));

        public ICqrsEsBuilder EnableSnapshots<TStore, TStrategy>() where TStore : ISnapshotStore where TStrategy : ISnapshotStrategy
            => EnableSnapshots(typeof(TStore), typeof(TStrategy));

        public ICqrsEsBuilder EnableSnapshots(Type snapshotStoreType)
            => EnableSnapshots(snapshotStoreType, typeof(DefaultSnapshotStrategy));

        public ICqrsEsBuilder EnableSnapshots(Type snapshotStoreType, Type snapshotStrategyType)
        {
            if (snapshotStoreType == null)
            {
                throw new ArgumentNullException(nameof(snapshotStoreType));
            }

            if (snapshotStrategyType == null)
            {
                throw new ArgumentNullException(nameof(snapshotStrategyType));
            }

            if (!typeof(ISnapshotStore).IsAssignableFrom(snapshotStoreType))
            {
                throw new ArgumentException($"Type {snapshotStoreType.Name} does not implement {nameof(ISnapshotStore)}.");
            }

            if (!typeof(ISnapshotStrategy).IsAssignableFrom(snapshotStrategyType))
            {
                throw new ArgumentException($"Type {snapshotStrategyType.Name} does not implement {nameof(ISnapshotStrategy)}.");
            }

            _enableSnapshots(snapshotStoreType, snapshotStrategyType);
            return _cqrsEsBuilder;
        }

        public ICqrsEsBuilder OverrideEventConverter<T>() where T : IEventConverter
            => OverrideEventConverter(typeof(T));

        public ICqrsEsBuilder OverrideEventConverter(Type type)
        {
            if (type == null)
            {
                throw new ArgumentNullException(nameof(type));
            }

            if (!typeof(IEventConverter).IsAssignableFrom(type))
            {
                throw new ArgumentException($"Type {type.Name} does not implement {nameof(IEventConverter)}.");
            }

            _overrideEventConverter(type);
            return _cqrsEsBuilder;
        }

        public ICqrsEsBuilder RegisterHandlers()
        {
            _enableHandlerRegistration();
            return _cqrsEsBuilder;
        }
    }
}