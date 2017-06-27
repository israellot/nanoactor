using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using MessagePack;

namespace NanoActor
{    

  
    [MessagePackObject]
    public class ActorResponse
    {
        [MessagePack.Key(0)]
        public Guid Id { get; set; }

        [MessagePack.Key(1)]
        public Boolean Success { get; set; }

        [MessagePack.Key(2)]
        public Boolean NotFound { get; set; }

        [MessagePack.Key(3)]
        public Exception Exception { get; set; }

        [MessagePack.Key(4)]
        public object Response { get; set; }
    }

    
}
