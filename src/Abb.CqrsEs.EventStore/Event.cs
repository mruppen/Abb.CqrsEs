using System;

namespace Abb.CqrsEs.EventStore
{
    public class Event : IEvent
    {
        public Event(Guid id, object data, DateTimeOffset timestamp, int version)
        {
            Id = id;
            Data = data;
            Timestamp = timestamp;
            Version = version;
        }

        public object Data { get; }

        public Guid Id { get; }

        public DateTimeOffset Timestamp { get; }

        public int Version { get; }
    }
}