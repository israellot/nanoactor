using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor
{
    public class ActorRequest
    {
        
        public Guid Id { get; set; }

        public String ActorInterface { get; set; }

        public String ActorId { get; set; }

        public String ActorMethodName { get; set; }

        public object[] Arguments { get; set; }

        public Boolean FromClient { get; set; }

        public ActorRequest() {
            this.Id = Guid.NewGuid();
            this.ActorId = String.Empty;
        }


    }
}
