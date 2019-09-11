using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs
{
    public interface IEventPersistence
    {
        Task Save(IEnumerable<Event> events, CancellationToken token = default);

        Task<Event[]> Get(Guid aggregateId, int fromVersion = 0, CancellationToken token = default);

        Task<Event> GetLastOrDefault(Guid aggregateId, CancellationToken token = default);
    }
}
