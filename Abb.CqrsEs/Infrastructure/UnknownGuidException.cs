using System;

namespace Abb.CqrsEs.Infrastructure
{
    public class UnknownGuidException : Exception
    {
        public UnknownGuidException(Guid unknownId)
            : this(unknownId, $"Guid {unknownId} is unknown.")

        { }

        public UnknownGuidException(Guid unknownGuid, string message)
            : base(message)
        {
            UnknownGuid = unknownGuid;
        }

        public UnknownGuidException(Guid unknownGuid, string message, Exception innerException)
            : base(message, innerException)
        {
            UnknownGuid = unknownGuid;
        }

        public Guid UnknownGuid { get; }
    }
}
