using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;

namespace NanoActor
{
    public class MsgPackTransportSerializer : ITransportSerializer
    {

        public MsgPackTransportSerializer()
        {
                      
       
        }

        public T Deserialize<T>(byte[] data)
        {
            if (data == null)
                return default(T);

            return MessagePackSerializer.Deserialize<T>(data,CustomCompositeResolver.Instance);
        }

        public byte[] Serialize(object o)
        {                            
           return MessagePackSerializer.Serialize((dynamic)o, CustomCompositeResolver.Instance);
        }
    }

    public class CustomCompositeResolver : IFormatterResolver
    {
        public static IFormatterResolver Instance = new CustomCompositeResolver();

        static readonly IFormatterResolver[] resolvers = new[]
        {
            // resolver custom types first
            MessagePack.Resolvers.BuiltinResolver.Instance,
            MessagePack.Resolvers.AttributeFormatterResolver.Instance,

            // replace enum resolver
            MessagePack.Resolvers.DynamicEnumAsStringResolver.Instance,

            MessagePack.Resolvers.DynamicGenericResolver.Instance,
            MessagePack.Resolvers.DynamicUnionResolver.Instance,
            MessagePack.Resolvers.DynamicObjectResolver.Instance,

            
            // final fallback(last priority)
            MessagePack.Resolvers.DynamicContractlessObjectResolver.Instance,
            
            // finaly use standard resolver
            StandardResolver.Instance
        };

        CustomCompositeResolver()
        {
        }

        public IMessagePackFormatter<T> GetFormatter<T>()
        {
            return FormatterCache<T>.formatter;
        }

        static class FormatterCache<T>
        {
            public static readonly IMessagePackFormatter<T> formatter;

            static FormatterCache()
            {
                foreach (var item in resolvers)
                {
                    var f = item.GetFormatter<T>();
                    if (f != null)
                    {
                        formatter = f;
                        return;
                    }
                }
            }
        }
    }
}
