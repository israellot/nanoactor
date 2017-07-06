using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor
{
    [MessagePackObject]
    public class StageAddress
    {
        [Key(0)]
        public String StageId { get; set; }

        [Key(1)]
        public SocketAddress SocketAddress { get; set; }

    }

    public class StageAddressQueryResponse
    {
        public Boolean Found { get; set; }

        public String StageId { get; set; }
    }
}
