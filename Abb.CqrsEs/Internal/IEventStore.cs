using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Internal
{
    /// <summary>
    /// Defines the methods of an event store.
    /// </summary>
    public interface IEventStore
    {
        /// <summary>
        /// Saves the pending events from an aggregate and publishes those events.
        /// </summary>
        /// <param name="aggregateId">The <see cref="Guid"/> of the aggregate.</param>
        /// <param name="events">The aggregate's pending events.</param>
        /// <param name="commitChanges">If specified, this function will be executed before any dispatching occurs.</param>
        /// <param name="eventPublisher">The event publisher instance.</param>
        /// <param name="token">A cancellation token.</param>
        /// <exception cref="ArgumentNullException">Thrown, when <paramref name="events"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown, when the operation could not be completed successfully.</exception>
        /// <returns>A <see cref="Task"/> object representing the operation.</returns>
        Task SaveAndPublish(Guid aggregateId, IEnumerable<Event> events, Func<CancellationToken, Task> commitChanges, IEventPersistence eventPersistence, CancellationToken token = default);

        /// <summary>
        /// Gets the latest version of an aggregate with id <paramref name="aggregateId"/>.
        /// </summary>
        /// <param name="aggregateId">The <see cref="Guid"/> of the aggregate.</param>
        /// <param name="token">A cancellation token.</param>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="aggregateId"/> is either <see cref="Guid.Empty"/> or default(Guid).</exception>
        /// <exception cref="Exceptions.UnknownGuidException">Thrown, when the <paramref name="aggregateId"/> is unknown.</exception>
        /// <exception cref="InvalidOperationException">Thrown, when the operation could not be completed successfully.</exception>
        /// <returns>A <see cref="Task{int}"/> object representing the operation.</returns>
        Task<int> GetVersion(Guid aggregateId, IEventPersistence eventPersistence, CancellationToken token = default);

        /// <summary>
        /// Gets all events of an aggregate with id <paramref name="aggregateId"/>.
        /// </summary>
        /// <param name="aggregateId">The <see cref="Guid"/> of the aggregate.</param>
        /// <param name="token">A cancellation token.</param>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="aggregateId"/> is either <see cref="Guid.Empty"/> or default(Guid).</exception>
        /// <exception cref="Exceptions.UnknownGuidException">Thrown, when the <paramref name="aggregateId"/> is unknown.</exception>
        /// <exception cref="InvalidOperationException">Thrown, when the operation could not be completed successfully.</exception>
        /// <returns>A <see cref="Task{IEnumerable{IEvent}}"/> object representing the operation.</returns>
        Task<IEnumerable<Event>> GetEvents(Guid aggregateId, IEventPersistence eventPersistence, CancellationToken token = default);

        /// <summary>
        /// Gets the events, starting with version <paramref name="fromVersion"/> of an aggregate with id <paramref name="aggregateId"/>.
        /// </summary>
        /// <param name="aggregateId">The <see cref="Guid"/> of the aggregate.</param>
        /// <param name="fromVersion">The starting version of the events.</param>
        /// <param name="token">A cancellation token.</param>
        /// <exception cref="ArgumentException">Thrown when the <paramref name="aggregateId"/> is either <see cref="Guid.Empty"/> or default(Guid).</exception>
        /// <exception cref="Exceptions.UnknownGuidException">Thrown, when the <paramref name="aggregateId"/> is unknown.</exception>
        /// <exception cref="InvalidOperationException">Thrown, when the operation could not be completed successfully.</exception>
        /// <returns>A <see cref="Task{IEnumerable{IEvent}}"/> object representing the operation.</returns>
        Task<IEnumerable<Event>> GetEvents(Guid aggregateId, int fromVersion, IEventPersistence eventPersistence, CancellationToken token = default);
    }
}
