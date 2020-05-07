using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Default
{
    public interface IEventCache
    {
        Task Add(string aggregateId, IEnumerable<Event> events, CancellationToken cancellationToken = default);

        Task Add(Event @event, CancellationToken cancellationToken = default);

        Task<bool> Delete(Event @event, CancellationToken cancellationToken = default);
    }
}