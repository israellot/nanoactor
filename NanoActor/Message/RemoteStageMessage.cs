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
        [MessagePack.Key(6)]
        public Boolean IsPingRequest { get; set; }
        [MessagePack.Key(7)]
        public Boolean IsPingReponse { get; set; }
        [MessagePack.Key(8)]
        public Ping Ping { get; set; }

    }

    [MessagePackObject]
    public class Ping
    {
        [MessagePack.Key(0)]
        public Guid Id { get; set; }
        [MessagePack.Key(1)]
        public long Timestamp { get; set; }

        public Ping()
        {
            this.Id = Guid.NewGuid();
            this.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        }
    }
}
