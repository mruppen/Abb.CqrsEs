using System;

namespace Abb.CqrsEs
{
    public interface ISnapshot
    {
        string AggregateId { get; set; }

        int Version { get; set; }
    }
}
