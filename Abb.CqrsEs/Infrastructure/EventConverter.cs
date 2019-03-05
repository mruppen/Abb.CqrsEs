using Newtonsoft.Json;

namespace Abb.CqrsEs.Infrastructure
{
    public class EventConverter : IEventConverter
    {
        private static readonly JsonSerializerSettings _serializerSettings = new JsonSerializerSettings()
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            DateFormatHandling = DateFormatHandling.IsoDateFormat,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            TypeNameHandling = TypeNameHandling.All
        };

        private static readonly JsonSerializerSettings _deserializerSettings = new JsonSerializerSettings()
        {
            DateTimeZoneHandling = DateTimeZoneHandling.Utc,
            TypeNameAssemblyFormatHandling = TypeNameAssemblyFormatHandling.Simple,
            TypeNameHandling = TypeNameHandling.All
        };

        public Event Convert(string payload)
        {
            if (string.IsNullOrEmpty(payload)) throw ExceptionHelper.ArgumentMustNotBeNullOrEmpty(nameof(payload));

            var @event = (Event)JsonConvert.DeserializeObject(payload, _deserializerSettings);
            return @event;
        }


        public string Convert(Event @event)
        {
            return JsonConvert.SerializeObject(@event ?? throw ExceptionHelper.ArgumentMustNotBeNull(nameof(@event)), _serializerSettings);
        }
    }
}
