using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor.Telemetry
{
    public class ExceptionTracker
    {
        ITelemetrySink[] _sinks;

        public ExceptionTracker(ITelemetrySink[] sinks, string name, TimeSpan? aggregationPeriod = null, IDictionary<string, string> properties = null)
        {
            this._sinks = sinks;
            
        }

        public void Track(Exception ex, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            foreach(var s in _sinks)
            {
                try
                {
                    s.TrackException(ex, properties, metrics);
                }
                catch { }
            }
        }

    }
}
