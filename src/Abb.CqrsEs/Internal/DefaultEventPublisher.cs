using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Internal
{
    public class DefaultEventPublisher : IEventPublisher
    {
        public Task Publish(Event @event, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
