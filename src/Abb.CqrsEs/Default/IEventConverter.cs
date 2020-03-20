namespace Abb.CqrsEs
{
    public interface IEventConverter
    {
        Event Convert(string payload);

        string Convert(Event @event);
    }
}
