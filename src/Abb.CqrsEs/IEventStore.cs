using System;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs
{
    public interface IEventStore
    {
        Task<EventStream> GetEventStream(string aggregateId, int fromVersion, CancellationToken cancellationToken = default);

        Task SaveAndPublish(EventStream eventStream, int expectedVersion, Action? commit, CancellationToken cancellationToken = default);
    }
}