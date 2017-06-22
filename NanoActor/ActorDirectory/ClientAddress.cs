using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.Directory
{


    public class ClientAddress
    {
        public Boolean NotFound { get; set; }
        
        public Boolean IsLocal { get; set; }

        public String Address { get; set; }
    }

    

   
}
