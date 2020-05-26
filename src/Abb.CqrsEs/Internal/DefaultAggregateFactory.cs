using System;

namespace Abb.CqrsEs.Internal
{
    public delegate object CreateAggregateRootDelegate(Type aggregateType);

    public class DefaultAggregateFactory : IAggregateFactory
    {
        private readonly CreateAggregateRootDelegate _createAggregateRootDelegate;

        public DefaultAggregateFactory(CreateAggregateRootDelegate createAggregateRootDelegate)
        {
            _createAggregateRootDelegate = createAggregateRootDelegate ?? throw new ArgumentNullException(nameof(createAggregateRootDelegate));
        }

        public T CreateAggregate<T>() where T : AggregateRoot => (T)_createAggregateRootDelegate(typeof(T));
    }
}