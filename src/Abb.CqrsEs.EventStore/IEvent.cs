using System;

namespace Abb.CqrsEs.EventStore
{
    public interface IEvent
    {
        public Guid Id { get; }

        DateTimeOffset Timestamp { get; }

        int Version { get; }
    }
}