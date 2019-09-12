using MediatR;
using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Abb.CqrsEs.Mediatr.UnitTests
{
    public class MediatrCommandHandler_UnitTests
    {
        [Fact]
        public async Task InvokeHandleMethod_process_method_is_invoked()
        {
            var invocationCount = 0;

            var handler = new TestMediatrCommandHandler(() => invocationCount++) as IRequestHandler<TestCommand>;
            var command = new TestCommand();

            var result = await handler.Handle(command, default);

            Assert.Equal(Unit.Value, result);
            Assert.Equal(1, invocationCount);
        }

        [Fact]
        public async Task InvokeProcessMethod_process_method_is_invoked()
        {
            var invocationCount = 0;

            var handler = new TestMediatrCommandHandler(() => invocationCount++) as ICommandHandler<TestCommand>;
            var command = new TestCommand();

            await handler.Process(command);

            Assert.Equal(1, invocationCount);
        }

        private class TestCommand : IMediatrCommand
        {
            public int ExpectedVersion => throw new NotImplementedException();

            public Guid CorrelationId => throw new NotImplementedException();
        }

        private class TestMediatrCommandHandler : MediatrCommandHandler<TestCommand>
        {
            private readonly Action _callback;

            public TestMediatrCommandHandler(Action callback)
            {
                _callback = callback;
            }

            public override Task Process(TestCommand command, CancellationToken cancellationToken = default)
            {
                _callback();
                return Task.CompletedTask;
            }
        }
    }
}
