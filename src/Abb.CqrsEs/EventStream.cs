using System.Collections.Generic;
using System.Linq;

namespace Abb.CqrsEs
{
    public class EventStream
    {
        public EventStream(string aggregateId, Event[] events)
        {
            AggregateId = aggregateId;
            FromVersion = events.FirstOrDefault()?.Version ?? AggregateRoot.InitialVersion;
            ToVersion = FromVersion + events.Length;
            Events = events;
        }

        public string AggregateId { get; }

        public IEnumerable<Event> Events { get; }

        public int FromVersion { get; }

        public int ToVersion { get; }
    }
}