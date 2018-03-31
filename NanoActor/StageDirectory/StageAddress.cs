using MessagePack;
using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor
{
    

    public class StageAddressQueryResponse
    {
        public Boolean Found { get; set; }

        public String StageId { get; set; }
    }
}
