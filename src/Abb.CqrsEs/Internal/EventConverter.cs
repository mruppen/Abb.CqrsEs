using Newtonsoft.Json;

namespace Abb.CqrsEs.Internal
{
    public class EventConverter : IEventConverter
    {
        private static readonly JsonSerializerSettings s_serializerSettings = new JsonSerializerSettings()
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            TypeNameHandling = TypeNameHandling.All
        };

        private static readonly JsonSerializerSettings s_deserializerSettings = new JsonSerializerSettings()
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            TypeNameHandling = TypeNameHandling.All
        };

        public Event Convert(string payload)
        {
            if (string.IsNullOrEmpty(payload))
            {
                throw ExceptionHelper.ArgumentMustNotBeNullOrEmpty(nameof(payload));
            }

            var @event = (Event)JsonConvert.DeserializeObject(payload, s_deserializerSettings);
            return @event;
        }


        public string Convert(Event @event)
        {
            return JsonConvert.SerializeObject(@event ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(@event)), s_serializerSettings);
        }
    }
}
