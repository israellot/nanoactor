using MessagePack;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.Directory
{

    [MessagePackObject]
    public class ClientAddress
    {
        [Key(0)]
        public Boolean NotFound { get; set; }

        [Key(1)]
        public Boolean IsLocal { get; set; }

        [Key(2)]
        public String Address { get; set; }
    }

    

   
}
