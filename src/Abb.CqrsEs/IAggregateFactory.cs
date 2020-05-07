namespace Abb.CqrsEs
{
    public interface IAggregateFactory
    {
        T CreateAggregate<T>() where T : AggregateRoot;
    }
}