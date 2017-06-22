using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;


namespace NanoActor
{    

  

    public class ActorResponse
    {
        public Guid Id { get; set; }
        public Boolean Success { get; set; }
        public Boolean NotFound { get; set; }

        public Exception Exception { get; set; }

        public object Response { get; set; }
    }

    
}
