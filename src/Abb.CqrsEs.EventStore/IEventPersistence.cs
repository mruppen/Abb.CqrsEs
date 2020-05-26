using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.EventStore
{
    public interface IEventPersistence
    {
        IAsyncEnumerable<Event> Get(string aggregateId, int fromVersion = 0, CancellationToken cancellationToken = default);

        Task<Event> GetLastOrDefault(string aggregateId, CancellationToken cancellationToken = default);

        Task Save(string aggregateId, IEnumerable<Event> eventStream, CancellationToken cancellationToken = default);
    }
}