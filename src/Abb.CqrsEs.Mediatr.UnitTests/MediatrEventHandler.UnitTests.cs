using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Abb.CqrsEs.Mediatr.UnitTests
{
    public class MediatrEventHandler_UnitTests
    {
        [Fact]
        public async Task InvokeHandleMethod_process_method_is_invoked()
        {
            var invocationCount = 0;

            var handler = new TestMediatrEventHandler(() => invocationCount++) as INotificationHandler<TestEvent>;
            var @event = new TestEvent();

            await handler.Handle(@event, default);

            Assert.Equal(1, invocationCount);
        }

        [Fact]
        public async Task InvokeProcessMethod_process_method_is_invoked()
        {
            var invocationCount = 0;

            var handler = new TestMediatrEventHandler(() => invocationCount++) as IEventHandler<TestEvent>;
            var @event = new TestEvent();

            await handler.Handle(@event);

            Assert.Equal(1, invocationCount);
        }

        private class TestEvent : IMediatrEvent
        {
            public Guid AggregateId => throw new NotImplementedException();

            public Guid CorrelationId => throw new NotImplementedException();

            public DateTimeOffset Timestamp => throw new NotImplementedException();

            public int Version => throw new NotImplementedException();
        }

        private class TestMediatrEventHandler : MediatrEventHandler<TestEvent>
        {
            private readonly Action _callback;

            public TestMediatrEventHandler(Action callback)
            {
                _callback = callback;
            }

            public override Task Handle(TestEvent command, CancellationToken cancellationToken = default)
            {
                _callback();
                return Task.CompletedTask;
            }
        }
    }
}