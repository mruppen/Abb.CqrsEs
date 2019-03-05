namespace Abb.CqrsEs.Infrastructure
{
    public interface IAggregateFactory
    {
        T CreateAggregate<T>() where T : AggregateRoot;
    }
}
