using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor
{

    [MessagePackObject]
    public class ActorRequest
    {

        [MessagePack.Key(0)]
        public Guid Id { get; set; }

        [MessagePack.Key(1)]
        public Boolean FireAndForget { get; set; }

        [MessagePack.Key(2)]
        public String ActorInterface { get; set; }

        [MessagePack.Key(3)]
        public String ActorId { get; set; }

        [MessagePack.Key(4)]
        public String ActorMethodName { get; set; }

        [MessagePack.Key(5)]
        public List<byte[]> Arguments { get; set; }

        [MessagePack.Key(6)]
        public Boolean FromClient { get; set; }

        public ActorRequest() {
            this.Id = Guid.NewGuid();
            this.ActorId = String.Empty;
        }


    }
}
