using System;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Infrastructure
{
    public interface IAggregateRepository
    {
        Task Save<T>(T aggregate, CancellationToken cancellationToken = default) where T : AggregateRoot;

        Task Save<T>(T aggregate, int expectedVersion, CancellationToken cancellationToken = default) where T : AggregateRoot;

        Task<T> Get<T>(Guid aggregateId, CancellationToken cancellationToken = default) where T : AggregateRoot;
    }
}
