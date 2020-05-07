using System;

namespace Abb.CqrsEs
{
    public interface IEvent : IMessage
    {
        DateTimeOffset Timestamp { get; }

        int Version { get; }
    }
}