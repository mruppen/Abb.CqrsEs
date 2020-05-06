using Abb.CqrsEs.Internal;
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
        private readonly IList<Event> _changes = new List<Event>();
        private readonly IDictionary<Type, (CompiledMethodInfo Handler, Type ReturnType)> _eventHandlers = new Dictionary<Type, (CompiledMethodInfo Handler, Type ReturnType)>();
        private readonly AsyncLock _lock = new AsyncLock();
        private string? _id;
        private bool _isCommitPending = false;

        protected AggregateRoot()
        {
            RegisterEventHandlers();
        }

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

        public virtual string Name
        {
            get { return GetType().Name; }
        }

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

        public int Version { get; protected set; }

        protected abstract ILogger Logger { get; }

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

        public async Task LoadFromHistory(string aggregateId, IEnumerable<Event> events, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            if (events == null)
            {
                throw ExceptionHelper.ArgumentMustNotBeNull(nameof(events));
            }

            VerifyEventStreamOrThrow(events);

            using (await _lock.Lock(token))
            {
                if (_isCommitPending)
                {
                    Logger.Warning(() => $"Aggregate {AggregateIdentifier} cannot load from history when a commit pending.");
                    throw new ConcurrencyException($"{AggregateIdentifier}: Cannot load history when a commit is pending.");
                }

                Id = aggregateId;

                Logger.Debug(() => $"Loading events for aggregate {AggregateIdentifier} from history.");
                foreach (var @event in events)
                {
                    token.ThrowIfCancellationRequested();
                    await ApplyEvent(@event, token);
                }
            }
        }

        protected async IAsyncEnumerable<Event> ConvertToAsyncEnumerable(Task<IEnumerable<Event>> events)
        {
            foreach (var e in await events)
            {
                yield return e;
            }
        }

        protected async IAsyncEnumerable<Event> ConvertToAsyncEnumerable(Task<Event> @event)
        {
            yield return await @event;
        }

        protected IAsyncEnumerable<Event> ConvertToAsyncEnumerable(IEnumerable<Event> events) => ConvertToAsyncEnumerable(Task.FromResult(events));

        protected Task Emit(Func<Task<Event>> createEventSynchronized, CancellationToken token = default)
        {
            if (createEventSynchronized == null)
            {
                throw ExceptionHelper.ArgumentMustNotBeNull(nameof(createEventSynchronized));
            }

            return DoEmit(() => ConvertToAsyncEnumerable(createEventSynchronized()), token);
        }

        protected Task Emit(Func<IAsyncEnumerable<Event>> createEventsSynchronized, CancellationToken token = default) => DoEmit(createEventsSynchronized, token);

        protected Task Emit(Func<Task<IEnumerable<Event>>> createEventsSynchronized, CancellationToken token = default)
        {
            if (createEventsSynchronized == null)
            {
                throw ExceptionHelper.ArgumentMustNotBeNull(nameof(createEventsSynchronized));
            }

            return DoEmit(() => ConvertToAsyncEnumerable(createEventsSynchronized()), token);
        }

        protected Task Emit<TEvent>(TEvent @event, CancellationToken token = default) where TEvent : Event => DoEmit(() => ConvertToAsyncEnumerable(Task.FromResult<Event>(@event)), token);

        protected Task Emit(IEnumerable<Event> events, CancellationToken token = default) => DoEmit(() => ConvertToAsyncEnumerable(Task.FromResult(events)), token);

        protected void ThrowIfExpectedVersionIsInvalid(ICommand command) => ThrowIfExpectedVersionIsInvalid(command?.ExpectedVersion ?? InitialVersion);

        protected void ThrowIfExpectedVersionIsInvalid(int expectedVersion)
        {
            if (expectedVersion != Version)
            {
                throw new ConcurrencyException($"Aggregate '{AggregateIdentifier}' has version {Version} but the command expected version {expectedVersion}.");
            }
        }

        private static bool CheckParameterType(Type expectedType, Type parameterType) => expectedType.IsAssignableFrom(parameterType)
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
            if (parameters == null || (parameters.Length != 1 && parameters.Length != 2))
            {
                return false;
            }

            var eventTypeParameterOk = CheckParameterType(typeof(Event), parameters[0].ParameterType);
            var cancellationTokenParameterOk = parameters.Length != 2 || CheckParameterType(typeof(CancellationToken), parameters[1].ParameterType);

            return eventTypeParameterOk && cancellationTokenParameterOk;
        }

        private async Task ApplyEvent(Event @event, CancellationToken token)
        {
            Logger.Debug(() => $"Applying event {@event.GetType().Name} with version {@event.Version} in aggregate {AggregateIdentifier}");

            if (_eventHandlers.TryGetValue(@event.GetType(), out var handlerInfo))
            {
                if (handlerInfo.ReturnType != typeof(Task))
                {
                    InvokeVoidHandler(handlerInfo.Handler, @event, token);
                }
                else
                {
                    await InvokeAsyncHandler(handlerInfo.Handler, @event, token);
                }
            }

            Version = @event.Version;
        }

        private async Task DoEmit(Func<IAsyncEnumerable<Event>> executeSynchronized, CancellationToken token = default)
        {
            token.ThrowIfCancellationRequested();

            using (await _lock.Lock(token))
            {
                if (_isCommitPending)
                {
                    Logger.Warning(() => $"Aggregate {AggregateIdentifier} cannot emit event when a commit pending.");
                    throw new ConcurrencyException($"{AggregateIdentifier}: Cannot emit an event when a commit is pending.");
                }

                await foreach (var @event in executeSynchronized())
                {
                    @event.AggregateId = Id;
                    @event.Version = Version + 1;
                    @event.Timestamp = DateTimeOffset.UtcNow;

                    await ApplyEvent(@event, token);
                    _changes.Add(@event);

                    Logger.Debug(() => $"Emitted event ({@event.GetType().Name},{@event.Version}.");
                }
            }
        }

        private Task InvokeAsyncHandler(CompiledMethodInfo handler, Event @event, CancellationToken token)
        {
            var result = handler.ParameterTypes.Length == 2 ? handler.Invoke(this, @event, token) : handler.Invoke(this, @event);
            return result == null ? Task.FromResult(false) : (Task)result;
        }

        private void InvokeVoidHandler(CompiledMethodInfo handler, Event @event, CancellationToken token)
        {
            if (handler.ParameterTypes.Length == 2)
            {
                handler.Invoke(this, @event, token);
            }
            else
            {
                handler.Invoke(this, @event);
            }
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

            var orderedEvents = events.OrderBy(@event => @event.Version);
            var firstEvent = orderedEvents.FirstOrDefault();
            if (firstEvent == null)
            {
                return;
            }

            if (firstEvent.Version != Version + 1)
            {
                throw new EventStreamException($"First event of event stream has invalid version (actual: {firstEvent.Version}, expected: {Version + 1})");
            }

            var previousVersion = firstEvent.Version;
            foreach (var @event in orderedEvents.Skip(1))
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