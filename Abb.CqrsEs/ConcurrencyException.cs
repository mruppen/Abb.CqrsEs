using System;

namespace Abb.CqrsEs
{
    public class ConcurrencyException : Exception
    {
        public ConcurrencyException()
            : base()
        { }

        public ConcurrencyException(string message)
            : base(message)
        { }
    }
}
