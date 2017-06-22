using NanoActor.Directory;
using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor
{
    public class RemoteStageMessage
    {
        public StageAddress Source { get; set; }

        public StageAddress Destination { get; set; }


        public Boolean IsActorResponse { get; set; }
        public ActorResponse ActorResponse { get; set; }


        public Boolean IsActorRequest { get; set; }

        public ActorRequest ActorRequest { get; set; }

    }
}
