using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor
{
    public class StageAddress
    {
        public String StageId { get; set; }

        public SocketAddress SocketAddress { get; set; }

        

    }

    public class StageAddressQueryResponse
    {
        public Boolean Found { get; set; }

        public String StageId { get; set; }
    }
}
