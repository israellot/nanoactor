using NanoActor.Directory;
using System;
using System.Collections.Generic;
using System.Text;
using MessagePack;

namespace NanoActor
{
    public enum RemoteMessageType
    {
        PingRequest = 0,
        PingResponse = 1,
        ActorRequest =2,
        ActorResponse=3,
        
    }

    [MessagePackObject]
    public class RemoteStageMessage
    {
        [Key(0)]
        public String Source { get; set; }

        [Key(1)]
        public String Destination { get; set; }

        [Key(2)]
        public RemoteMessageType MessageType { get; set; }

        [Key(3)]
        public ActorResponse ActorResponse { get; set; }
        
        [Key(4)]
        public ActorRequest ActorRequest { get; set; }
       
        [Key(5)]
        public Ping Ping { get; set; }

    }

    [MessagePackObject]
    public class Ping
    {
        [Key(0)]       
        public Guid Id { get; set; }
        [Key(1)]
        public long Timestamp { get; set; }

        public Ping()
        {
            this.Id = Guid.NewGuid();
            this.Timestamp = DateTimeOffset.UtcNow.Ticks;
        }
    }
}
