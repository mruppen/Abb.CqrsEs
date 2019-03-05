using System;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Infrastructure
{
    public class DefaultEventPublisher : IEventPublisher
    {
        public Task Publish(IEvent @event, Func<CancellationToken, Task> next, CancellationToken token = default)
        {
            return next(token);
        }
    }
}
