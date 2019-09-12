using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs
{
    public abstract class AggregateRoot
    {
        private readonly AsyncLock _lock = new AsyncLock();
        private readonly IDictionary<Type, (CompiledMethodInfo Handler, Type ReturnType)> _eventHandlers = new Dictionary<Type, (CompiledMethodInfo Handler, Type ReturnType)>();
        private readonly IList<Event> _changes = new List<Event>();

        private Guid? _id;
        private bool _isCommitPending = false;

        protected AggregateRoot()
        {
            RegisterEventHandlers();
        }

        protected abstract ILogger Logger { get; }

        public static int InitialVersion { get { return 0; } }

        public Guid Id
        {
            get { return _id ?? Guid.Empty; }
            set
            {
                if (_id.HasValue && _id.Value != Guid.Empty && _id != value)
                {
                    Logger.Warning(() => $"Cannot replace aggregate id of {AggregateIdentifier}.");
                    throw new InvalidOperationException($"{AggregateIdentifier}: Cannot set id to {value}.");
                }
                Logger.Debug(() => $"Id set for aggregate {AggregateIdentifier}.");
                _id = value;
            }
        }

        public int Version { get; protected set; }

        public int PendingChangesCount
        {
            get
            {
                using (_lock.Lock().GetAwaiter().GetResult())
                {
                    return _changes.Count;
                }
            }
        }

        public virtual string Name
        {
            get { return GetType().Name; }
        }

        public string AggregateIdentifier
        {
            get { return $"{Name} ({Id},{Version})"; }
        }

        public async Task<Event[]> GetPendingChanges(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            using (await _lock.Lock(token))
            {
                _isCommitPending = true;
                var result = _changes.ToArray();
                Logger.Debug(() => $"Aggregate {AggregateIdentifier} has {result.Length} pending changes.");
                return result;
            }
        }

        public async Task CommitChanges(CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            using (await _lock.Lock(token))
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
        }

        public async Task LoadFromHistory(IEnumerable<Event> events, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            if (events == null) throw ExceptionHelper.ArgumentMustNotBeNull(nameof(events));
            VerifyEventStreamOrThrow(events);

            using (await _lock.Lock(token))
            {
                if (_isCommitPending)
                {
                    Logger.Warning(() => $"Aggregate {AggregateIdentifier} cannot load from history when a commit pending.");
                    throw new ConcurrencyException($"{AggregateIdentifier}: Cannot load history when a commit is pending.");
                }

                var first = events.FirstOrDefault();
                if (first != null)
                {
                    Id = first.AggregateId;

                    Logger.Debug(() => $"Loading events for aggregate {AggregateIdentifier} from history.");
                    await @events.ForEachAsync(async @event =>
                    {
                        token.ThrowIfCancellationRequested();
                        await ApplyEvent(@event, token);
                    });
                }
            }
        }

        protected Task Emit(Func<Task<Event>> createEventSynchronized, CancellationToken token = default)
        {
            if (createEventSynchronized == null)
                throw ExceptionHelper.ArgumentMustNotBeNull(nameof(createEventSynchronized));

            return DoEmit(createEventSynchronized, token);
        }

        protected Task Emit<TEvent>(TEvent @event, Func<TEvent, Task> executeSynchronized = null, CancellationToken token = default) where TEvent : Event
        {
            return DoEmit(() =>
            {
                if (executeSynchronized != null)
                    return executeSynchronized(@event).Then(() => Task.FromResult<Event>(@event));
                return Task.FromResult<Event>(@event);
            }, token);
        }

        protected Task Emit<TEvent>(TEvent @event, Action<TEvent> executeSynchronized = null, CancellationToken token = default) where TEvent : Event
        {
            return DoEmit(() =>
            {
                executeSynchronized?.Invoke(@event);
                return Task.FromResult<Event>(@event);
            }, token);
        }

        protected Task Emit<TEvent>(TEvent @event, CancellationToken token = default) where TEvent : Event
        {
            return DoEmit(() => Task.FromResult<Event>(@event));
        }

        private async Task DoEmit(Func<Task<Event>> executeSynchronized, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            using (await _lock.Lock(token))
            {
                if (_isCommitPending)
                {
                    Logger.Warning(() => $"Aggregate {AggregateIdentifier} cannot emit event when a commit pending.");
                    throw new ConcurrencyException($"{AggregateIdentifier}: Cannot emit an event when a commit is pending.");
                }
                var @event = await executeSynchronized();

                @event.AggregateId = Id;
                @event.Version = Version + 1;
                @event.Timestamp = DateTime.UtcNow;

                await ApplyEvent(@event, token);
                _changes.Add(@event);

                Logger.Debug(() => $"Emitted event ({@event.GetType().Name},{@event.Version}.");
            }
        }

        private async Task ApplyEvent(Event @event, CancellationToken token)
        {
            Logger.Debug(() => $"Applying event {@event.GetType().Name} with version {@event.Version} in aggregate {AggregateIdentifier}");

            if (_eventHandlers.TryGetValue(@event.GetType(), out var handlerInfo))
            {
                if (handlerInfo.ReturnType != typeof(Task))
                    InvokeVoidHandler(handlerInfo.Handler, @event, token);
                else
                    await InvokeAsyncHandler(handlerInfo.Handler, @event, token);
            }

            Version = @event.Version;
        }

        private void InvokeVoidHandler(CompiledMethodInfo handler, Event @event, CancellationToken token)
        {
            if (handler.ParameterTypes.Length == 2)
                handler.Invoke(this, @event, token);
            else
                handler.Invoke(this, @event);
        }

        private Task InvokeAsyncHandler(CompiledMethodInfo handler, Event @event, CancellationToken token)
        {
            if (handler.ParameterTypes.Length == 2)
                return (Task)handler.Invoke(this, @event, token);
            else
                return (Task)handler.Invoke(this, @event);
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
                return;

            var orderedEvents = events.OrderBy(@event => @event.Version);
            var firstEvent = orderedEvents.FirstOrDefault();
            if (firstEvent == null)
                return;

            if (firstEvent.Version != Version + 1)
                throw new EventStreamException($"First event of event stream has invalid version (actual: {firstEvent.Version}, expected: {Version + 1})");

            var previousVersion = firstEvent.Version;
            foreach (var @event in orderedEvents.Skip(1))
            {
                if (@event.Version != previousVersion + 1)
                    throw new EventStreamException($"An event of the event stream has an invalid version (actual: {@event.Version}, expected: {previousVersion + 1}");
                previousVersion = @event.Version;
            }
        }

        private static bool IsEventHandler(MethodInfo methodInfo)
        {
            if (methodInfo == null)
                return false;

            var parameters = methodInfo.GetParameters();
            if (parameters == null || (parameters.Length != 1 && parameters.Length != 2))
                return false;

            var eventTypeParameterOk = CheckParameterType(typeof(Event), parameters[0].ParameterType);
            var cancellationTokenParameterOk = parameters.Length == 2
                ? CheckParameterType(typeof(CancellationToken), parameters[1].ParameterType)
                : true;

            return eventTypeParameterOk && cancellationTokenParameterOk;
        }

        private static bool CheckParameterType(Type expectedType, Type parameterType)
        {
            return expectedType.IsAssignableFrom(parameterType)
                && !parameterType.IsInterface
                && !parameterType.IsAbstract
                && !parameterType.IsGenericParameter;
        }
    }
}