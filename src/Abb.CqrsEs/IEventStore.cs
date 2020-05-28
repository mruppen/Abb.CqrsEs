using System;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs
{
    public interface IEventStore
    {
        Task<EventStream> GetEventStream(string aggregateId, int fromVersion, CancellationToken cancellationToken = default);

        Task<int> GetVersion(string aggregateId, CancellationToken cancellationToken = default);

        Task SaveAndPublish(EventStream eventStream, Action? commit, CancellationToken cancellationToken = default);
    }
}