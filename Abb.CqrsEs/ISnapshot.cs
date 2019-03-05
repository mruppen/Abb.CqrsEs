using System;

namespace Abb.CqrsEs
{
    public interface ISnapshot
    {
        Guid AggregateId { get; set; }

        int Version { get; set; }
    }
}
