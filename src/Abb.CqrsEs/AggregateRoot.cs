using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Abb.CqrsEs
{
    public abstract class AggregateRoot
    {
        private readonly IList<Event> _changes = new List<Event>();
        private string? _id;
        private bool _isCommitPending = false;

        public static int InitialVersion { get { return 0; } }

        public string AggregateIdentifier
        {
            get { return $"{Name} ({Id},{Version})"; }
        }

        public string Id
        {
            get { return _id ?? string.Empty; }
            set
            {
                if (!string.IsNullOrEmpty(_id) && _id != value)
                {
                    Logger.Warning(() => $"Cannot replace aggregate id of {AggregateIdentifier}.");
                    throw new InvalidOperationException($"{AggregateIdentifier}: Cannot set id to {value}.");
                }
                Logger.Debug(() => $"Id set for aggregate {AggregateIdentifier}.");
                _id = value;
            }
        }

        public virtual string Name => GetType().Name;

        public int PendingChangesCount => _changes.Count;

        public int Version { get; protected set; }

        protected abstract ILogger Logger { get; }

        public void CommitChanges()
        {
            if (!_isCommitPending)
            {
                Logger.Warning(() => $"Cannot commit aggregate {AggregateIdentifier} when there's no commit pending.");
                throw new ConcurrencyException($"{AggregateIdentifier}: Cannot commit changes when no commit is pending.");
            }
            _changes.Clear();
            _isCommitPending = false;
            Logger.Debug(() => $"Committed changes for aggregate {AggregateIdentifier}");
        }

        public EventStream GetPendingChanges()
        {
            _isCommitPending = true;
            Logger.Debug(() => $"Aggregate {AggregateIdentifier} has {_changes.Count} pending changes.");
            return new EventStream(Id, _changes.ToArray());
        }

        public void Load(EventStream eventStream)
        {
            if (eventStream == null)
            {
                throw new ArgumentNullException(nameof(eventStream));
            }

            if (_isCommitPending)
            {
                Logger.Warning(() => $"Aggregate {AggregateIdentifier} cannot load from history when a commit pending.");
                throw new ConcurrencyException($"{AggregateIdentifier}: Cannot load history when a commit is pending.");
            }

            VerifyEventStreamOrThrow(eventStream.Events);

            Id = eventStream.AggregateId;

            Logger.Debug(() => $"Loading events for aggregate {AggregateIdentifier} from history.");
            foreach (var @event in eventStream.Events)
            {
                ApplyEvent(@event);
            }
        }

        protected void Emit(Guid correlationId, object @event)
        {
            if (_isCommitPending)
            {
                Logger.Warning(() => $"Aggregate {AggregateIdentifier} cannot emit event when a commit pending.");
                throw new ConcurrencyException($"{AggregateIdentifier}: Cannot emit an event when a commit is pending.");
            }

            var wrappedEvent = new Event(correlationId, @event, DateTimeOffset.UtcNow, Version + 1);

            ApplyEvent(wrappedEvent);
            _changes.Add(wrappedEvent);

            Logger.Debug(() => $"Emitted event ({@event.GetType().Name},{wrappedEvent.Version}.");
        }

        protected void ThrowIfExpectedVersionIsInvalid(ICommand command) => ThrowIfExpectedVersionIsInvalid(command?.ExpectedVersion ?? InitialVersion);

        protected void ThrowIfExpectedVersionIsInvalid(int expectedVersion)
        {
            if (expectedVersion != Version)
            {
                throw new ConcurrencyException($"Aggregate '{AggregateIdentifier}' has version {Version} but the command expected version {expectedVersion}.");
            }
        }

        protected abstract void When(object @event);

        private void ApplyEvent(Event @event)
        {
            Logger.Debug(() => $"Applying event with version {@event.Version} to aggregate {AggregateIdentifier}");

            When(@event.Data);

            Version = @event.Version;
        }

        private void VerifyEventStreamOrThrow(IEnumerable<Event> events)
        {
            if (events == null)
            {
                return;
            }

            var firstEvent = events.FirstOrDefault();
            if (firstEvent == null)
            {
                return;
            }

            if (firstEvent.Version != Version + 1)
            {
                throw new EventStreamException($"First event of event stream has invalid version (actual: {firstEvent.Version}, expected: {Version + 1})");
            }

            var previousVersion = firstEvent.Version;
            foreach (var @event in events.Skip(1))
            {
                if (@event.Version != previousVersion + 1)
                {
                    throw new EventStreamException($"An event of the event stream has an invalid version (actual: {@event.Version}, expected: {previousVersion + 1}");
                }

                previousVersion = @event.Version;
            }
        }
    }
}