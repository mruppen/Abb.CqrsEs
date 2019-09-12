using System.Threading;
using System.Threading.Tasks;

namespace Abb.CqrsEs
{
    public interface ICommandHandler<in TCommand> where TCommand : ICommand
    {
        Task Process(TCommand command, CancellationToken cancellationToken = default);
    }
}
