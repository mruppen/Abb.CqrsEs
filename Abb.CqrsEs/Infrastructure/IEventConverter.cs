namespace Abb.CqrsEs.Infrastructure
{
    public interface IEventConverter
    {
        Event Convert(string payload);

        string Convert(Event @event);
    }
}
