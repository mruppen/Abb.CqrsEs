namespace Abb.CqrsEs.Infrastructure
{
    public interface IPersistedEvent
    {
        Event Event { get; set; }
    }
}