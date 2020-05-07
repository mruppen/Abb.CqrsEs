using System;

namespace Abb.CqrsEs
{
    public interface ISnapshotStrategy
    {
        bool IsSnapshottable(Type aggregateRootType);

        bool TakeSnapshot<T>(T aggregateRoot, int pendingChangesCount) where T : AggregateRoot;
    }
}