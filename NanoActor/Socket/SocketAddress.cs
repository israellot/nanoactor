using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor
{
    [MessagePackObject]
    public class SocketAddress
    {
        [MessagePack.Key(0)]
        public String Address { get; set; }
        [MessagePack.Key(1)]
        public String Scheme { get; set; }
        [MessagePack.Key(2)]
        public Boolean IsClient { get; set; }
        [MessagePack.Key(3)]
        public Boolean IsStage { get; set; }
        [MessagePack.Key(4)]
        public String StageId { get; set; }

    }
}
