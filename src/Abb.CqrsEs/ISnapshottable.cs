using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs
{
    public interface ISnapshottable
    {
        Task<ISnapshot> CreateSnapshot(CancellationToken token = default);

        Task RestoreSnapshot(ISnapshot snapshot, CancellationToken token = default);
    }
}