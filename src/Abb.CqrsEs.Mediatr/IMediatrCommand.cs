using MediatR;

namespace Abb.CqrsEs.Mediatr
{
    public interface IMediatrCommand : ICommand, IRequest
    {
    }
}