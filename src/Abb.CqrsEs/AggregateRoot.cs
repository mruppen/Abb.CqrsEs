using Abb.CqrsEs.Internal;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Abb.CqrsEs
{
    public abstract class AggregateRoot
    {
        private readonly IList<Event> _changes = new List<Event>();
        private readonly IDictionary<Type, (CompiledMethodInfo Handler, Type ReturnType)> _eventHandlers = new Dictionary<Type, (CompiledMethodInfo Handler, Type ReturnType)>();
        private string? _id;
        private bool _isCommitPending = false;

        protected AggregateRoot() => RegisterEventHandlers();

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

        protected void Emit(Event @event)
        {
            if (_isCommitPending)
            {
                Logger.Warning(() => $"Aggregate {AggregateIdentifier} cannot emit event when a commit pending.");
                throw new ConcurrencyException($"{AggregateIdentifier}: Cannot emit an event when a commit is pending.");
            }

            @event.Version = Version + 1;
            @event.Timestamp = DateTimeOffset.UtcNow;

            ApplyEvent(@event);
            _changes.Add(@event);

            Logger.Debug(() => $"Emitted event ({@event.GetType().Name},{@event.Version}.");
        }

        protected void ThrowIfExpectedVersionIsInvalid(ICommand command) => ThrowIfExpectedVersionIsInvalid(command?.ExpectedVersion ?? InitialVersion);

        protected void ThrowIfExpectedVersionIsInvalid(int expectedVersion)
        {
            if (expectedVersion != Version)
            {
                throw new ConcurrencyException($"Aggregate '{AggregateIdentifier}' has version {Version} but the command expected version {expectedVersion}.");
            }
        }

        private static bool CheckParameterType(Type expectedType, Type parameterType)
            => expectedType.IsAssignableFrom(parameterType)
                && !parameterType.IsInterface
                && !parameterType.IsAbstract
                && !parameterType.IsGenericParameter;

        private static bool IsEventHandler(MethodInfo methodInfo)
        {
            if (methodInfo == null)
            {
                return false;
            }

            var parameters = methodInfo.GetParameters();
            if (parameters == null || parameters.Length != 1)
            {
                return false;
            }

            var eventTypeParameterOk = CheckParameterType(typeof(Event), parameters[0].ParameterType);
            return eventTypeParameterOk;
        }

        private void ApplyEvent(Event @event)
        {
            Logger.Debug(() => $"Applying event {@event.GetType().Name} with version {@event.Version} in aggregate {AggregateIdentifier}");

            if (_eventHandlers.TryGetValue(@event.GetType(), out var handlerInfo))
            {
                handlerInfo.Handler.Invoke(this, @event);
            }

            Version = @event.Version;
        }

        private void RegisterEventHandlers()
        {
            var thisType = GetType();
            var methods = thisType.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic);

            methods.Where(IsEventHandler)
                   .Select(mi => new KeyValuePair<Type, (CompiledMethodInfo Handler, Type ReturnType)>(mi.GetParameters()[0].ParameterType, (new CompiledMethodInfo(mi, thisType), mi.ReturnType)))
                   .ForEach(_eventHandlers.Add);
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