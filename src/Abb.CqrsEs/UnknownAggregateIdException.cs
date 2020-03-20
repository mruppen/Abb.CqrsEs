using System;

namespace Abb.CqrsEs
{
    public class UnknownAggregateIdException : Exception
    {
        public UnknownAggregateIdException(Guid unknownId)
            : this(unknownId, $"Guid {unknownId} is unknown.")

        { }

        public UnknownAggregateIdException(Guid unknownGuid, string message)
            : base(message)
        {
            UnknownGuid = unknownGuid;
        }

        public UnknownAggregateIdException(Guid unknownGuid, string message, Exception innerException)
            : base(message, innerException)
        {
            UnknownGuid = unknownGuid;
        }

        public Guid UnknownGuid { get; }
    }
}
