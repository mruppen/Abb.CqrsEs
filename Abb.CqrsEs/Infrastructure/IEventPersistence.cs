using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Infrastructure
{
    public interface IEventPersistence
    {
        Task<IPersistedEvent[]> Save(IEnumerable<Event> events, CancellationToken token = default);

        Task SetDispatched(IPersistedEvent persistedEvent, CancellationToken token = default);

        Task SetPublished(IPersistedEvent persistedEvent, CancellationToken token = default);

        Task<Event[]> Get(Guid aggregateId, int fromVersion = 0, CancellationToken token = default);

        Task<Event> GetLastOrDefault(Guid aggregateId, CancellationToken token = default);

        Task<IPersistedEvent[]> GetUndispatched(CancellationToken token = default);

        Task<IPersistedEvent[]> GetUnpublished(CancellationToken token = default);
    }
}
