namespace Abb.CqrsEs
{
    public interface ICommand : IMessage
    {
        int ExpectedVersion { get; }
    }
}
