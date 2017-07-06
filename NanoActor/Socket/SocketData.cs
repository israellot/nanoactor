using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor
{
    [MessagePackObject]
    public class SocketData
    {
        [Key(0)]
        public SocketAddress Address { get; set; }

        [Key(1)]
        public byte[] Data { get; set; }
    }
}
