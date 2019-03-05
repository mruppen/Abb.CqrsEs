using System;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Infrastructure
{
    public interface IAggregateRepository
    {
        Task Save<T>(T aggregate, CancellationToken token = default) where T : AggregateRoot;

        Task Save<T>(T aggregate, int expectedVersion, CancellationToken token = default) where T : AggregateRoot;

        Task<T> Get<T>(Guid aggregateId, CancellationToken token = default) where T : AggregateRoot;
    }
}
