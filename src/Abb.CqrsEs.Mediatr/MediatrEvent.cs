using System;

namespace Abb.CqrsEs.Mediatr
{
    public abstract class MediatrEvent : Event, IMediatrEvent
    {
        protected MediatrEvent(Guid correlationId) : base(correlationId)
        {
        }
    }
}
