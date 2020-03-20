using System.Collections.Generic;

namespace Abb.CqrsEs
{
    public class EventStream
    {
        public EventStream(string aggregateId, int fromVersion, Event[] events)
        {
            AggregateId = aggregateId;
            FromVersion = fromVersion;
            ToVersion = fromVersion + events.Length;
            Events = events;
        }

        public string AggregateId { get; }

        public IEnumerable<Event> Events { get; }

        public int FromVersion { get; }

        public int ToVersion { get; }
    }
}
