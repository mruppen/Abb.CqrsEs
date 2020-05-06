using System;

namespace Abb.CqrsEs
{
    public class UnknownAggregateIdException : Exception
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
