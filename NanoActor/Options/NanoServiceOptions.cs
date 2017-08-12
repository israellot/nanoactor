using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor.Options
{
    public class NanoServiceOptions
    {
        public String ServiceName { get; set; } = "default";
        public Boolean TrackProxyDependencyCalls { get; set; } = false;
        public Boolean TrackActorExecutionDependencyCalls { get; set; } = false;

        public int DefaultActorTTL { get; set; } = 60 * 60 * 24;

        public int DefaultProxyTimeout { get; set; } = 15;
    }
}
