using System;

namespace Abb.CqrsEs
{
    public class EventStreamException : Exception
    {
        public EventStreamException(string message)
            : base(message)
        {
        }
    }
}
