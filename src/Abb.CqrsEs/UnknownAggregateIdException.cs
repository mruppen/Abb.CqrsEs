using System;

namespace Abb.CqrsEs
{
#pragma warning disable S3925 // "ISerializable" should be implemented correctly

    public class UnknownAggregateIdException : Exception
#pragma warning restore S3925 // "ISerializable" should be implemented correctly
    {
        public UnknownAggregateIdException(string unknownId)
            : this(unknownId, $"Id {unknownId} is unknown.")

        { }

        public UnknownAggregateIdException(string unknownGuid, string message)
            : base(message)
            => UnknownGuid = unknownGuid;

        public UnknownAggregateIdException(string unknownGuid, string message, Exception innerException)
            : base(message, innerException)
            => UnknownGuid = unknownGuid;

        public string UnknownGuid { get; }
    }
}