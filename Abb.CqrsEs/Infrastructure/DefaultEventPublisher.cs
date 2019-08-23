using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Infrastructure
{
    public class DefaultEventPublisher : IEventPublisher
    {
        public Task Publish(Event @event, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }
    }
}
