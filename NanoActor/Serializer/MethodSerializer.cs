using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NanoActor.Serializer
{
    public class MethodSerializer
    {
        ITransportSerializer _serializer;
        public MethodSerializer(ITransportSerializer serializer)
        {
            this._serializer = serializer;
        }


        public byte[] SerializeArguments(object[] args)
        {           
            List<byte[]> arguments = new List<byte[]>();
            foreach (var argument in args)
                arguments.Add(_serializer.Serialize(argument));

            return _serializer.Serialize(arguments);
        }


    }
}
