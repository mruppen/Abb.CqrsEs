using MediatR;

namespace Abb.CqrsEs.Mediatr
{
    public interface IMediatrEvent : IEvent, INotification
    { }
}
