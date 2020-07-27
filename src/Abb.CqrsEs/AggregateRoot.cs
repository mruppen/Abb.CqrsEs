using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Abb.CqrsEs
{
    public abstract class AggregateRoot
    {
        private readonly IList<object> _changes = new List<object>();
        private string? _id;
        private bool _isCommitPending = false;

        protected AggregateRoot() => Version = InitialVersion;

        public static int InitialVersion => -1;

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

        public object[] GetPendingChanges()
        {
            _isCommitPending = true;
            Logger.Debug(() => $"Aggregate {AggregateIdentifier} has {_changes.Count} pending changes.");
            return _changes.ToArray();
        }

        public void Load(IEnumerable<object> events)
        {
            if (events == null)
            {
                throw new ArgumentNullException(nameof(events));
            }

            if (_isCommitPending)
            {
                Logger.Warning(() => $"Aggregate {AggregateIdentifier} cannot load from history when a commit pending.");
                throw new ConcurrencyException($"{AggregateIdentifier}: Cannot load history when a commit is pending.");
            }

            Logger.Debug(() => $"Loading events for aggregate {AggregateIdentifier} from history.");
            foreach (var @event in events)
            {
                ApplyEvent(@event);
            }
        }

        protected void Emit(object @event)
        {
            if (_isCommitPending)
            {
                Logger.Warning(() => $"Aggregate {AggregateIdentifier} cannot emit event when a commit pending.");
                throw new ConcurrencyException($"{AggregateIdentifier}: Cannot emit an event when a commit is pending.");
            }

            ApplyEvent(@event);
            _changes.Add(@event);

            Logger.Debug(() => $"Emitted event '{@event.GetType().Name}'");
        }

        protected void ThrowIfExpectedVersionIsInvalid(int expectedVersion)
        {
            if (expectedVersion != Version)
            {
                throw new ConcurrencyException($"Aggregate '{AggregateIdentifier}' has version {Version} but the command expected version {expectedVersion}.");
            }
        }

        protected abstract void When(object @event);

        private void ApplyEvent(object @event)
        {
            Logger.Debug(() => $"Applying event {@event.GetType().Name} to aggregate {AggregateIdentifier}");

            When(@event);
            Version++;
        }
    }
}