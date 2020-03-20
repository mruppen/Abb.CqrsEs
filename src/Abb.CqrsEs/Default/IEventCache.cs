using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Default
{
    public interface IEventCache
    {
        Task Add(Event @event, CancellationToken cancellationToken = default);

        Task<IEnumerable<IGrouping<Guid, Event>>> GetAll(CancellationToken cancellationToken = default);

        Task<bool> Delete(Event @event, CancellationToken cancellationToken = default);
    }
}
