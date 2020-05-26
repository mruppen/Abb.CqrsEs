using System;

namespace Abb.CqrsEs
{
    public class Event : IEvent
    {
        public Event(Guid correlationId, object data, DateTimeOffset timestamp, int version)
        {
            CorrelationId = correlationId;
            Data = data;
            Timestamp = timestamp;
            Version = version;
        }

        public Guid CorrelationId { get; }

        public object Data { get; }

        public DateTimeOffset Timestamp { get; }

        public int Version { get; }
    }
}