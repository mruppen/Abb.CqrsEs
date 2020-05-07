using System;

namespace Abb.CqrsEs
{
    public abstract class Event : IEvent
    {
        protected Event(Guid correlationId)
        {
            CorrelationId = correlationId;
        }

        public Guid CorrelationId { get; internal set; }

        public DateTimeOffset Timestamp { get; internal set; }

        public int Version { get; internal set; }
    }
}