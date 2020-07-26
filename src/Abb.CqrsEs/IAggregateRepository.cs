using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs
{
    public interface IAggregateRepository
    {
        Task<T> Get<T>(string aggregateId, CancellationToken cancellationToken = default) where T : AggregateRoot;

        Task Save<T>(T aggregate, int expectedVersion = int.MinValue, CancellationToken cancellationToken = default) where T : AggregateRoot;
    }
}