using System;

namespace Abb.CqrsEs
{
#pragma warning disable S3925 // "ISerializable" should be implemented correctly

    public class EventStreamException : Exception
#pragma warning restore S3925 // "ISerializable" should be implemented correctly
    {
        public EventStreamException(string message)
            : base(message)
        {
        }
    }
}