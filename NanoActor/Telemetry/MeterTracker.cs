using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NanoActor.Telemetry
{
    public class MeterTracker
    {

        Lazy<Meter> meter;

        ITelemetrySink[] _sinks;
        public MeterTracker(ITelemetrySink[] sinks, string name,TimeSpan? aggregationPeriod=null)
        {
            this._sinks = sinks;
            this.meter = new Lazy<Meter>(() => { return new Meter(name, sinks, aggregationPeriod); });
        }

        public void Tick()
        {
            meter.Value.Tick();
        }

        public List<MeterDataPoint> GetHistory()
        {
            return meter.Value.History.ToList();
        }

    }

}
