using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs
{
    public interface IEventStore
    {
        Task<IEnumerable<Event>> GetEvents(string aggregateId, CancellationToken token = default);

        Task<IEnumerable<Event>> GetEvents(string aggregateId, int fromVersion, CancellationToken token = default);

        Task<int> GetVersion(string aggregateId, CancellationToken token = default);

        Task SaveAndPublish(EventStream eventStream, Func<CancellationToken, Task> commitChanges, CancellationToken token = default);
    }
}
