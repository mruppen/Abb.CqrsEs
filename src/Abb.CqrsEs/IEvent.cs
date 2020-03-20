using System;

namespace Abb.CqrsEs
{
    public interface IEvent : IMessage
    {
        string AggregateId { get; }

        int Version { get; }

        DateTimeOffset Timestamp { get; }
    }
}
