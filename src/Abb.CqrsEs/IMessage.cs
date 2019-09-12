using System;

namespace Abb.CqrsEs
{
    public interface IMessage
    {
        Guid CorrelationId { get; }
    }
}
