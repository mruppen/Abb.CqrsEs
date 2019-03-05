using System;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Infrastructure
{
    public interface ISnapshotStore
    {
        Task Save(ISnapshot snapshot, CancellationToken token = default);

        Task<ISnapshot> Get(Guid id, CancellationToken token = default);
    }
}
