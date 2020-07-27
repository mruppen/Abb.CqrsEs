namespace Abb.CqrsEs
{
    public class EventStream
    {
        public EventStream(string aggregateId, int fromVersion, object[] events)
        {
            AggregateId = aggregateId;
            FromVersion = fromVersion;
            Events = events;
        }

        public string AggregateId { get; }

        public object[] Events { get; }

        public int FromVersion { get; }
    }
}