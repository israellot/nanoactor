using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor
{
    public class SocketAddress
    {
        public String Address { get; set; }

        public String Scheme { get; set; }

        public Boolean IsClient { get; set; }

        public Boolean IsStage { get; set; }
    }
}
