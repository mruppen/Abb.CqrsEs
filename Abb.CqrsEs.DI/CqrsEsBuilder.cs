using Abb.CqrsEs.Internal;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Abb.CqrsEs.DI.Lamar")]

namespace Abb.CqrsEs.DI
{
    internal class CqrsEsBuilder<TServices> : ICqrsEsBuilder where TServices : IServiceCollection
    {
        protected readonly TServices _services;
        private readonly Lazy<ICqrsEsOptionalBuilder> _optionalBuilder = new Lazy<ICqrsEsOptionalBuilder>();

        private Type _eventCacheType, _eventPersistenceType, _eventPublisherType, _snapshotStoreType, _snapshotStrategyType;
        private Type _eventConverterType = typeof(EventConverter);
        private bool _enableHandlerRegistration;

        public CqrsEsBuilder(TServices services)
        {
            _services = services;
            _optionalBuilder = new Lazy<ICqrsEsOptionalBuilder>(() => new CqrsEsOptionalBuilder(this, t => OverrideEventConverter(t), (s, t) => EnableSnapshots(s, t), () => _enableHandlerRegistration = true));
        }

        protected virtual bool ContainerSupportFuncInjection { get { return false; } }

        public ICqrsEsOptionalBuilder Optional => _optionalBuilder.Value;

        public ICqrsEsBuilder AddEventCache<T>() where T : IEventCache
            => AddEventCache(typeof(T));

        public ICqrsEsBuilder AddEventCache(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!typeof(IEventCache).IsAssignableFrom(type))
                throw new ArgumentException($"Type {type.Name} does not implement {nameof(IEventCache)}.");

            _eventCacheType = type;
            return this;
        }

        public ICqrsEsBuilder AddEventPersistence<T>() where T : IEventPersistence
            => AddEventPersistence(typeof(T));

        public ICqrsEsBuilder AddEventPersistence(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!typeof(IEventPersistence).IsAssignableFrom(type))
                throw new ArgumentException($"Type {type.Name} does not implement {nameof(IEventPersistence)}.");

            _eventPersistenceType = type;
            return this;
        }

        public ICqrsEsBuilder AddEventPublisher<T>() where T : IEventPublisher
            => AddEventPublisher(typeof(T));

        public ICqrsEsBuilder AddEventPublisher(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!typeof(IEventPublisher).IsAssignableFrom(type))
                throw new ArgumentException($"Type {type.Name} does not implement {nameof(IEventPublisher)}.");

            _eventPublisherType = type;
            return this;
        }

        public TServices Build()
        {
            if (_eventCacheType == null || _eventConverterType == null || _eventPersistenceType == null || _eventPublisherType == null)
                throw new InvalidOperationException("One or more types are not configured correctly.");

            var services = _services.AddSingleton<IEventStore, EventStore>()
                .AddSingleton<IAggregateFactory, DefaultAggregateFactory>()
                .AddSingleton(typeof(IEventCache), _eventCacheType)
                .AddSingleton(typeof(IEventConverter), _eventConverterType)
                .AddSingleton(typeof(IEventPublisher), _eventPublisherType)
                .AddScoped(typeof(IEventPersistence), _eventPersistenceType)
                .AddSingleton(typeof(CreateAggregateRootDelegate), p =>
                {
                    CreateAggregateRootDelegate func = t => ActivatorUtilities.GetServiceOrCreateInstance(p, t);
                    return func;
                });

            if (!ContainerSupportFuncInjection)
            {
                services.AddSingleton(typeof(Func<IEventPublisher>), p =>
                {
                    Func<IEventPublisher> func = () => p.GetRequiredService<IEventPublisher>();
                    return func;

                });
            }

            if (_snapshotStoreType != null)
            {
                services.AddScoped(typeof(ISnapshotStore), _snapshotStoreType)
                    .AddScoped(typeof(ISnapshotStrategy), _snapshotStrategyType)
                    .AddScoped(typeof(IAggregateInteractionService), p =>
                    {
                        var basicRepository = ActivatorUtilities.GetServiceOrCreateInstance<AggregateInteractionService>(p);
                        return ActivatorUtilities.CreateInstance<AggregateSnapshotInteractionService>(p, basicRepository);
                    });
            }
            else
            {
                services.AddScoped<IAggregateInteractionService, AggregateInteractionService>();
            }

            if (_enableHandlerRegistration)
                RegisterEventHandlers();

            return (TServices)services;
        }

        protected virtual void RegisterEventHandlers()
        {
            var types = GetAllExportedTypes();

            var eventHandler = typeof(IEventHandler<>).GetTypeInfo();
            var commandHandler = typeof(ICommandHandler<>).GetTypeInfo();

            foreach (var t in types)
            {
                foreach (var handler in GetGenericInterfaceImplementations(t.GetTypeInfo(), eventHandler))
                {
                    _services.AddTransient(handler, t);
                }

                foreach (var handler in GetGenericInterfaceImplementations(t.GetTypeInfo(), commandHandler))
                {
                    _services.AddTransient(handler, t);
                }
            }
        }

        private ICqrsEsBuilder EnableSnapshots(Type snapshotStoreType, Type snapshotStrategyType)
        {
            if (snapshotStoreType == null)
                throw new ArgumentNullException(nameof(snapshotStoreType));

            if (snapshotStrategyType == null)
                throw new ArgumentNullException(nameof(snapshotStrategyType));

            if (!typeof(ISnapshotStore).IsAssignableFrom(snapshotStoreType))
                throw new ArgumentException($"Type {snapshotStoreType.Name} does not implement {nameof(ISnapshotStore)}.");

            if (!typeof(ISnapshotStrategy).IsAssignableFrom(snapshotStrategyType))
                throw new ArgumentException($"Type {snapshotStrategyType.Name} does not implement {nameof(ISnapshotStrategy)}.");

            _snapshotStoreType = snapshotStoreType;
            _snapshotStrategyType = snapshotStrategyType;
            return this;
        }

        private ICqrsEsBuilder OverrideEventConverter(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            if (!typeof(IEventConverter).IsAssignableFrom(type))
                throw new ArgumentException($"Type {type.Name} does not implement {nameof(IEventConverter)}.");

            _eventConverterType = type;
            return this;
        }

        private IEnumerable<Type> GetAllExportedTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies().Where(a => !a.IsDynamic)
                .SelectMany(a => a.ExportedTypes)
                .Where(t => !t.IsAbstract && !t.IsInterface);
        }

        private IEnumerable<Type> GetGenericInterfaceImplementations(TypeInfo type, Type @interface)
        {
            if (type.IsGenericTypeDefinition)
            {
                return type.ImplementedInterfaces.Where(i => i.IsGenericType && !i.IsGenericTypeDefinition && i.GetGenericTypeDefinition() == @interface).Select(i => i.GetGenericTypeDefinition());
            }

            var interfaces = type.ImplementedInterfaces.ToArray();
            return GetClosedGenericInterface(interfaces, @interface).Concat(GetOpenGenericInterfaces(interfaces, @interface)).Distinct();
        }

        private IEnumerable<Type> GetClosedGenericInterface(Type[] implementedInterfaces, Type @interface)
            => implementedInterfaces.Where(t => t.IsGenericType && !t.IsGenericTypeDefinition && t.GetGenericTypeDefinition() == @interface);

        private IEnumerable<Type> GetOpenGenericInterfaces(Type[] implementedInterfaces, Type @interface)
            => implementedInterfaces.Where(t => t.IsGenericTypeDefinition && t == @interface);
    }
}
