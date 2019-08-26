using MediatR;
using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs.Mediatr
{
    public abstract class MediatrCommandHandler<TCommand> : IMediatrCommandHandler<TCommand> where TCommand : IMediatrCommand
    {
        public Task<Unit> Handle(TCommand request, CancellationToken cancellationToken)
            => Process(request, cancellationToken).Then(() => Unit.Task);

        public abstract Task Process(TCommand command, CancellationToken cancellationToken = default);
    }
}