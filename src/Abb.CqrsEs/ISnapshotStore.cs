using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs
{
    public interface ISnapshotStore
    {
        Task<ISnapshot?> Get(string aggregateId, CancellationToken token = default);

        Task Save(ISnapshot snapshot, CancellationToken token = default);
    }
}