using MediatR;

namespace Abb.CqrsEs.Mediatr
{
    public interface IMediatrEventHandler<in TEvent> : IEventHandler<TEvent>, INotificationHandler<TEvent> where TEvent : IMediatrEvent
    {
    }
}