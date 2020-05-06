using Abb.CqrsEs.Internal;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Abb.CqrsEs.DI.UnitTests
{
    public class CqrsEsServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddCqrsEs_add_all_required_services_works_as_expected()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddCqrsEs(builder =>
            {
                builder.AddEventCache<DummyEventCache>()
                    .AddEventPersistence<DummyEventPersistence>()
                    .AddEventPublisher<DummyEventPublisher>();
            });

            var provider = services.BuildServiceProvider();

            var repository = provider.GetService<IAggregateRepository>();

            Assert.NotNull(repository);
            Assert.IsType<AggregateRepository>(repository);
        }

        [Fact]
        public void AddCqrsEs_enable_snapshots_works_as_expected()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddCqrsEs(builder =>
            {
                builder.AddEventCache<DummyEventCache>()
                    .AddEventPersistence<DummyEventPersistence>()
                    .AddEventPublisher<DummyEventPublisher>()
                    .Optional.EnableSnapshots<DummySnapshotStore>();
            });

            var provider = services.BuildServiceProvider();

            var repository = provider.GetService<IAggregateRepository>();

            Assert.NotNull(repository);
            Assert.IsType<AggregateSnapshotRepositoryDecorator>(repository);
        }

        [Fact]
        public void AddCqrsEs_enable_handler_registrations_works_as_expected()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            services.AddCqrsEs(builder =>
            {
                builder.AddEventCache<DummyEventCache>()
                    .AddEventPersistence<DummyEventPersistence>()
                    .AddEventPublisher<DummyEventPublisher>()
                    .Optional.RegisterHandlers();
            });

            var provider = services.BuildServiceProvider();

            var testEvent1Handlers = provider.GetServices<IEventHandler<TestEvent1>>()?.ToArray();

            Assert.NotNull(testEvent1Handlers);
            Assert.Equal(3, testEvent1Handlers.Length);
            Assert.Contains(testEvent1Handlers, i => i.GetType() == typeof(SingleClosedEventHandler));
            Assert.Contains(testEvent1Handlers, i => i.GetType() == typeof(MultipleClosedEventHandler));
            Assert.Contains(testEvent1Handlers, i => i.GetType() == typeof(OpenEventHandler<TestEvent1>));

            var testEvent2Handlers = provider.GetServices<IEventHandler<TestEvent2>>()?.ToArray();

            Assert.NotNull(testEvent2Handlers);
            Assert.Equal(2, testEvent2Handlers.Length);
            Assert.Contains(testEvent2Handlers, i => i.GetType() == typeof(MultipleClosedEventHandler));
            Assert.Contains(testEvent2Handlers, i => i.GetType() == typeof(OpenEventHandler<TestEvent2>));


            var commandHandlers = provider.GetServices(typeof(ICommandHandler<TestCommand>))?.ToArray();

            Assert.NotNull(commandHandlers);
            Assert.Equal(2, commandHandlers.Length);
            Assert.Contains(commandHandlers, i => i.GetType() == typeof(ClosedCommandHandler));
            Assert.Contains(commandHandlers, i => i.GetType() == typeof(OpenCommandHandler<TestCommand>));
        }

        [Fact]
        public void AddCqrsEs_missing_event_cache_type_throws_exception()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            Assert.Throws<InvalidOperationException>(() => services.AddCqrsEs(builder =>
            {
                builder.AddEventPersistence<DummyEventPersistence>()
                    .AddEventPublisher<DummyEventPublisher>();
            }));
        }

        [Fact]
        public void AddCqrsEs_invalid_event_cache_type_throws_exception()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            Assert.Throws<ArgumentException>(() => services.AddCqrsEs(builder =>
            {
                builder.AddEventCache(typeof(DummyEventPublisher))
                    .AddEventPersistence<DummyEventPersistence>()
                    .AddEventPublisher<DummyEventPublisher>();
            }));
        }

        [Fact]
        public void AddCqrsEs_missing_event_persistence_type_throws_exception()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            Assert.Throws<InvalidOperationException>(() => services.AddCqrsEs(builder =>
            {
                builder.AddEventCache(typeof(DummyEventCache))
                    .AddEventPublisher<DummyEventPublisher>();
            }));
        }

        [Fact]
        public void AddCqrsEs_invalid_event_persistence_type_throws_exception()
        {
            var services = new ServiceCollection();
            services.AddLogging();

            Assert.Throws<ArgumentException>(() => services.AddCqrsEs(builder =>
            {
                builder.AddEventCache(typeof(DummyEventCache))
                    .AddEventPersistence(typeof(DummyEventCache))
                    .AddEventPublisher<DummyEventPublisher>();
            }));
        }
    }

    public class DummyEventCache : IEventCache
    {
        public Task Add(Event @event, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<bool> Delete(Event @event, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task<IEnumerable<IGrouping<Guid, Event>>> GetAll(CancellationToken cancellationToken = default) => Task.FromResult(new IGrouping<Guid, Event>[0] as IEnumerable<IGrouping<Guid, Event>>);
    }

    public class DummyEventPersistence : IEventPersistence
    {
        public Task<Event[]> Get(Guid aggregateId, int fromVersion = 0, CancellationToken token = default) => throw new NotImplementedException();

        public Task<Event> GetLastOrDefault(Guid aggregateId, CancellationToken token = default) => throw new NotImplementedException();

        public Task Save(IEnumerable<Event> events, CancellationToken token = default) => throw new NotImplementedException();
    }

    public class DummyEventPublisher : IEventPublisher
    {
        public Task Publish(Event @event, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    public class DummySnapshotStore : ISnapshotStore
    {
        public Task<ISnapshot> Get(Guid id, CancellationToken token = default) => throw new NotImplementedException();

        public Task Save(ISnapshot snapshot, CancellationToken token = default) => throw new NotImplementedException();
    }

    public class TestCommand : Command
    {
        public TestCommand(Guid correlationId, int expectedVersion) : base(correlationId, expectedVersion)
        {
        }
    }

    public class ClosedCommandHandler : ICommandHandler<TestCommand>
    {
        public Task Process(TestCommand command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    public class OpenCommandHandler<T> : ICommandHandler<T> where T : ICommand
    {
        public Task Process(T command, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    public class TestEvent1 : Event
    {
        public TestEvent1(Guid correlationId) : base(correlationId)
        {
        }
    }

    public class TestEvent2 : Event
    {
        public TestEvent2(Guid correlationId) : base(correlationId)
        {
        }
    }

    public class SingleClosedEventHandler : IEventHandler<TestEvent1>
    {
        public Task Handle(TestEvent1 @event, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    public class MultipleClosedEventHandler : IEventHandler<TestEvent1>, IEventHandler<TestEvent2>
    {
        public Task Handle(TestEvent2 @event, CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public Task Handle(TestEvent1 @event, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }

    public class OpenEventHandler<T> : IEventHandler<T> where T : IEvent
    {
        public Task Handle(T @event, CancellationToken cancellationToken = default) => throw new NotImplementedException();
    }
}
