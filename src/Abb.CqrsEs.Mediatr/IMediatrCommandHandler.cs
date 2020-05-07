using MediatR;

namespace Abb.CqrsEs.Mediatr
{
    public interface IMediatrCommandHandler<in TCommand> : ICommandHandler<TCommand>, IRequestHandler<TCommand> where TCommand : IMediatrCommand
    {
    }
}