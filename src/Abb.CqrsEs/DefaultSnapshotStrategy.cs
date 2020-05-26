using System;
using System.Linq;
using System.Reflection;

namespace Abb.CqrsEs
{
    public class DefaultSnapshotStrategy : ISnapshotStrategy
    {
        public const int MinimumEventsBetweenSnapshots = 20;

        public bool IsSnapshottable(Type aggregateRootType) => aggregateRootType.GetTypeInfo().GetInterfaces().Any(i => i == typeof(ISnapshottable));

        public bool TakeSnapshot<T>(T aggregateRoot, int pendingChangesCount) where T : AggregateRoot
        {
            if (pendingChangesCount == 0)
            {
                return false;
            }

            if (!IsSnapshottable(typeof(T)))
            {
                return false;
            }

            var previousVersion = aggregateRoot.Version - pendingChangesCount;
            for (var i = 1; i <= pendingChangesCount; i++)
            {
                if ((previousVersion + i) % MinimumEventsBetweenSnapshots == 0)
                {
                    return true;
                }
            }

            return false;
        }
    }
}