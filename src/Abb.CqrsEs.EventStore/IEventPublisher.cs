using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.EventStore
{
    public interface IEventPublisher
    {
        Task Publish(EventStream eventStream, CancellationToken cancellationToken = default);
    }
}