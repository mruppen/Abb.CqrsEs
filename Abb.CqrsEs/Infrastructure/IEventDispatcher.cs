using System;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Infrastructure
{
    public interface IEventDispatcher
    {
        Task Dispatch(IEvent @event, Func<CancellationToken, Task> next, CancellationToken token = default);
    }
}
