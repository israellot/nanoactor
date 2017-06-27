using MessagePack;
using NanoActor.Directory;
using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor
{

    [MessagePackObject]
    public class RemoteClientMessage
    {
        [MessagePack.Key(0)]
        public ClientAddress Source { get; set; }

        [MessagePack.Key(1)]
        public ClientAddress Destination { get; set; }

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
