using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs
{
    public interface IEventHandler<in TEvent> where TEvent : IEvent
    {
        Task Handle(TEvent @event, CancellationToken cancellationToken = default);
    }
}
