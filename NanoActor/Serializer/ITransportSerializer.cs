using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor
{
    public interface ITransportSerializer
    {
        byte[] Serialize<T>(T o);

        byte[] Serialize(object o);

        T Deserialize<T>(byte[] data);

        object Deserialize(Type type, byte[] data);

    }
}
