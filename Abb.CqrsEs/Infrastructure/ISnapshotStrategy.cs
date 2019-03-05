using System;

namespace Abb.CqrsEs.Infrastructure
{
    public interface ISnapshotStrategy
    {
        bool IsSnapshottable(Type aggregateRootType);

        bool TakeSnapshot<T>(T aggregateRoot) where T : AggregateRoot;
    }
}
