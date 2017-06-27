using NanoActor.Directory;
using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;

namespace NanoActor
{
    [MessagePackObject]
    public class RemoteStageMessage
    {
        [MessagePack.Key(0)]
        public SocketAddress Source { get; set; }

        [MessagePack.Key(1)]
        public SocketAddress Destination { get; set; }

        [MessagePack.Key(2)]
        public Boolean IsActorResponse { get; set; }

        [MessagePack.Key(3)]
        public ActorResponse ActorResponse { get; set; }

        [MessagePack.Key(4)]
        public Boolean IsActorRequest { get; set; }
        
        [MessagePack.Key(5)]
        public ActorRequest ActorRequest { get; set; }

    }
}
