using System;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Infrastructure
{
    public interface IEventPublisher
    {
        Task Publish(IEvent @event, Func<CancellationToken, Task> next, CancellationToken token = default);
    }
}
