using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor
{
    public class JsonTransportSerializer : ITransportSerializer
    {
        JsonSerializerSettings _jsonSettings;

        public JsonTransportSerializer()
        {

            _jsonSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore,
                DateFormatHandling=DateFormatHandling.IsoDateFormat,
                DefaultValueHandling=DefaultValueHandling.Ignore                
            };

        }

        public T Deserialize<T>(byte[] data)
        {
            if (data == null)
                return default(T);

            var dataString = Encoding.UTF8.GetString(data);

            return JsonConvert.DeserializeObject<T>(dataString, _jsonSettings);
        }

        public byte[] Serialize(object o)
        {            
            var jsonString = JsonConvert.SerializeObject(o, _jsonSettings);

            return Encoding.UTF8.GetBytes(jsonString);
        }
    }
}
