using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor
{
    public interface ITransportSerializer
    {
        byte[] Serialize(object o);

        T Deserialize<T>(byte[] data);

    }
}
