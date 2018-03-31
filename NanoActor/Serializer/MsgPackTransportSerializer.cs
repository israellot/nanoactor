using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;
using MessagePack.Formatters;
using MessagePack.Resolvers;
using System.Reflection;
using System.Linq;

namespace NanoActor
{
    public class MsgPackTransportSerializer : ITransportSerializer
    {

        MethodInfo _genericSerializer;
        MethodInfo _genericDeserializer;


        public MsgPackTransportSerializer()
        {

            _genericSerializer= typeof(MsgPackTransportSerializer)
              .GetMethods()
              .Single(m => m.Name == "Serialize" && m.IsGenericMethodDefinition);

            _genericDeserializer = typeof(MsgPackTransportSerializer)
              .GetMethods()
              .Single(m => m.Name == "Deserialize" && m.IsGenericMethodDefinition);

            
        }

        public T Deserialize<T>(byte[] data)
        {
            if (data == null)
                return default(T);
            try
            {
                return LZ4MessagePackSerializer.Deserialize<T>(data, CustomCompositeResolver.Instance);
            }
            catch(Exception ex)
            {
                try
                {
                    return MessagePackSerializer.Deserialize<T>(data, CustomCompositeResolver.Instance);
                }
                catch
                {
                    return default(T);
                }

            }
            
        }

        public object Deserialize(Type type,byte[] data)
        {
            var methodInfo = _genericDeserializer.MakeGenericMethod(type);

            var obj = methodInfo.Invoke(this, new[] { data });

            return obj;

        }

        public byte[] Serialize<T>(T o)
        {
            try
            {                
                return LZ4MessagePackSerializer.Serialize(o, CustomCompositeResolver.Instance);
                
            }
            catch(Exception ex)
            {
                return null;
            }
         
        }

        public byte[] Serialize(object o)
        {
            if (o == null)
                return LZ4MessagePackSerializer.Serialize<Object>(null);

            var methodInfo = _genericSerializer.MakeGenericMethod(o.GetType());

            var data = (Byte[])methodInfo.Invoke(this, new[] { o });

            return data;
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

            ContractlessStandardResolver.Instance,

            // finaly use standard resolver
            StandardResolver.Instance,

           
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
