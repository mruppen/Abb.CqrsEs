using System;

namespace Abb.CqrsEs.UnitTests.Common
{
    public abstract class EventWrapper : Event
    {
        private static readonly Action<Event, DateTimeOffset> s_setTimestampAction = (obj, @value) => typeof(Event).GetProperty("Timestamp").GetSetMethod(true).Invoke(obj, new object[] { @value });
        private static readonly Action<Event, int> s_setVersionAction = (obj, @value) => typeof(Event).GetProperty("Version").GetSetMethod(true).Invoke(obj, new object[] { @value });

        protected EventWrapper(Guid correlationId)
            : base(correlationId)
        { }

        protected EventWrapper(Guid correlationId, int version)
            : base(correlationId)
        {
            s_setVersionAction(this, version);
        }

        public void SetTimestamp(DateTimeOffset dateTime) => s_setTimestampAction(this, dateTime);

        public void SetVersion(int version) => s_setVersionAction(this, version);
    }
}