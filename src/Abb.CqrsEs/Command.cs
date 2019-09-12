using System;

namespace Abb.CqrsEs
{
    public abstract class Command : ICommand
    {
        protected Command(Guid correlationId, int expectedVersion)
        {
            CorrelationId = correlationId;
            ExpectedVersion = expectedVersion;
        }

        public int ExpectedVersion { get; }

        public Guid CorrelationId { get; }
    }
}
