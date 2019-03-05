using System;

namespace Abb.CqrsEs
{
    public interface IEvent : IMessage
    {
        Guid AggregateId { get; }

        int Version { get; }

        DateTimeOffset Timestamp { get; }
    }
}
