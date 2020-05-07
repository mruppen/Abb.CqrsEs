using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.EventStore
{
    public interface IEventPersistence
    {
        Task<Event[]> Get(string aggregateId, int fromVersion = 0, CancellationToken token = default);

        Task<Event> GetLastOrDefault(string aggregateId, CancellationToken token = default);

        Task Save(EventStream eventStream, CancellationToken token = default);
    }
}