using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor
{
    public class JsonTransportSerializer : ITransportSerializer
    {
        public T Deserialize<T>(byte[] data)
        {
            if (data == null)
                return default(T);

            var dataString = Encoding.UTF8.GetString(data);

            return JsonConvert.DeserializeObject<T>(dataString);
        }

        public byte[] Serialize(object o)
        {            
            var jsonString = JsonConvert.SerializeObject(o);

            return Encoding.UTF8.GetBytes(jsonString);
        }
    }
}
