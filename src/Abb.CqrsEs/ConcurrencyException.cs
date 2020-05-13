using System;

namespace Abb.CqrsEs
{
#pragma warning disable S3925 // "ISerializable" should be implemented correctly

    public class ConcurrencyException : Exception
#pragma warning restore S3925 // "ISerializable" should be implemented correctly
    {
        public ConcurrencyException()
            : base()
        { }

        public ConcurrencyException(string message)
            : base(message)
        { }
    }
}