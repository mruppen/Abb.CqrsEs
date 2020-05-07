namespace Abb.CqrsEs.Internal
{
    public class EventConverter : IEventConverter
    {
        private static readonly JsonSerializerSettings s_deserializerSettings = new JsonSerializerSettings()
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            TypeNameHandling = TypeNameHandling.All
        };

        private static readonly JsonSerializerSettings s_serializerSettings = new JsonSerializerSettings()
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            TypeNameHandling = TypeNameHandling.All
        };

        public Event Convert(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                throw new ArgumentNullException(nameof(payload));
            }

            var @event = (Event)JsonConvert.DeserializeObject(payload, s_deserializerSettings);
            return @event;
        }

        public string Convert(Event @event) => JsonConvert.SerializeObject(@event ?? throw new ArgumentNullException(nameof(@event)), s_serializerSettings);
    }
}