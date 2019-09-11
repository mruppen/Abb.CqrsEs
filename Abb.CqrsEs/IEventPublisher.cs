using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs
{
    public interface IEventPublisher
    {
        Task Publish(Event @event, CancellationToken cancellationToken = default);
    }
}
