using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Mediatr
{
    public abstract class MediatrEventHandler<TEvent> : IMediatrEventHandler<TEvent> where TEvent : IMediatrEvent
    {
        public abstract Task Handle(TEvent @event, CancellationToken cancellationToken = default);
    }
}