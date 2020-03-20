using System;
using Newtonsoft.Json;

namespace Abb.CqrsEs
{
    public abstract class Event : IEvent
    {
        protected Event(Guid correlationId)
        {
            CorrelationId = correlationId;
        }

        [JsonProperty("aggregateId")]
        public string AggregateId { get; internal set; } = string.Empty;

        [JsonProperty("version")]
        public int Version { get; internal set; }

        [JsonProperty("timestamp")]
        public DateTimeOffset Timestamp { get; internal set; }

        [JsonProperty("correlationId")]
        public Guid CorrelationId { get; internal set; }
    }
}
