using System;
using Xunit;

namespace Abb.CqrsEs.UnitTests.Internal
{
    public class EventConverterTests
    {
        [Fact]
        public void EventConverter_conversion_works_in_both_directions()
        {
            var correlationId = Guid.NewGuid();
            var aggregateId = Guid.NewGuid();
            var version = 1;

            var event1 = new Event1(correlationId);
            event1.SetAggregateId(aggregateId);
            event1.SetVersion(version);
            event1.SetTimestamp(DateTimeOffset.UtcNow);

            var converter = new EventConverter();

            var payload = converter.Convert(event1);
            Assert.False(string.IsNullOrEmpty(payload));

            var convertedEvent1 = converter.Convert(payload) as Event1;
            Assert.NotNull(convertedEvent1);
            Assert.Equal(correlationId, convertedEvent1.CorrelationId);
            Assert.Equal(aggregateId, convertedEvent1.AggregateId);
            Assert.Equal(version, convertedEvent1.Version);
            Assert.Equal(event1.Timestamp, convertedEvent1.Timestamp);
        }

        public class Event1 : EventWrapper
        {
            public Event1(Guid correlationId) : base(correlationId)
            { }

            public string Text { get; set; }
        }

        public class Event2 : EventWrapper
        {
            public Event2(string text, Guid correlationId) : base(correlationId)
            {
                Text = text;
            }

            public string Text { get; }
        }
    }
}