using System;

namespace Abb.CqrsEs.Internal
{
    public class DefaultSnapshotStrategy : ISnapshotStrategy
    {
        public const int MinimumEventsBetweenSnapshots = 20;

        public bool IsSnapshottable(Type aggregateRootType)
        {
            return aggregateRootType.ImplementsInterface<ISnapshottable>();
        }

        public bool TakeSnapshot<T>(T aggregateRoot) where T : AggregateRoot
        {
            if (!IsSnapshottable(typeof(T)))
                return false;

            var pendingChangesCount = aggregateRoot.PendingChangesCount;
            if (pendingChangesCount == 0)
                return false;

            var previousVersion = aggregateRoot.Version - pendingChangesCount;
            for (var i = 1; i <= pendingChangesCount; i++)
            {
                if ((previousVersion + i) % MinimumEventsBetweenSnapshots == 0)
                    return true;
            }

            return false;
        }
    }
}
