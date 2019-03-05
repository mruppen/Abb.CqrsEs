using System;

namespace Abb.CqrsEs.UnitTests.Common
{
    public abstract class EventWrapper : Event
    {
        private static Action<Event, int> s_setVersionAction;
        private static Action<Event, Guid> s_setAggregateIdAction;
        private static readonly Action<Event, DateTimeOffset> s_setTimestampAction;

        static EventWrapper()
        {
            var versionProperty = typeof(Event).GetProperty("Version");
            var setVersionMethod = versionProperty.GetSetMethod(true);
            s_setVersionAction = (obj, @value) => setVersionMethod.Invoke(obj, new object[] { @value });

            var aggregateIdProperty = typeof(Event).GetProperty("AggregateId");
            var setAggregateIdMethod = aggregateIdProperty.GetSetMethod(true);
            s_setAggregateIdAction = (obj, @value) => setAggregateIdMethod.Invoke(obj, new object[] { @value });

            var timestampProperty = typeof(Event).GetProperty("Timestamp");
            var setTimestampMethod = timestampProperty.GetSetMethod(true);
            s_setTimestampAction = (obj, @value) => setTimestampMethod.Invoke(obj, new object[] { @value });
        }

        protected EventWrapper(Guid correlationId)
            : base(correlationId)
        { }

        protected EventWrapper(Guid correlationId, int version)
            : base(correlationId)
        {
            s_setVersionAction(this, version);
        }

        protected EventWrapper(Guid correlationId, Guid aggregateId)
            : base(correlationId)
        {
            s_setAggregateIdAction(this, aggregateId);
        }

        protected EventWrapper(Guid correlationId, int version, Guid aggregateId)
            : base(correlationId)
        {
            s_setVersionAction(this, version);
            s_setAggregateIdAction(this, aggregateId);
        }

        public void SetVersion(int version)
        {
            s_setVersionAction(this, version);
        }

        public void SetAggregateId(Guid aggregateId)
        {
            s_setAggregateIdAction(this, aggregateId);
        }

        public void SetTimestamp(DateTimeOffset dateTime)
        {
            s_setTimestampAction(this, dateTime);
        }
    }
}
