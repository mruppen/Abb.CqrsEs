using System;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs
{
    public interface IEventStore
    {
        Task<EventStream> GetEventStream(string aggregateId, int fromVersion, CancellationToken token = default);

        Task<EventStream> GetEventStream(string aggregateId, CancellationToken token = default);

        Task<int> GetVersion(string aggregateId, CancellationToken token = default);

        Task SaveAndPublish(EventStream eventStream, Action commit, CancellationToken token = default);
    }
}