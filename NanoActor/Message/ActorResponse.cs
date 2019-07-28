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
        [Key(0)]
        public Guid Id { get; set; }

        [Key(1)]
        public Boolean Success { get; set; }

        [Key(2)]
        public Boolean NotFound { get; set; }

        [Key(3)]
        public Exception Exception { get; set; }

        [Key(4)]
        public byte[] Response { get; set; }

        
    }

    
}
