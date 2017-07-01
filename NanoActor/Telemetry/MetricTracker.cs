using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace NanoActor.Telemetry
{
    public class MetricTracker
    {

        Lazy<Metric> metric;

        ITelemetrySink[] _sinks;
        public MetricTracker(ITelemetrySink[] sinks,string name)
        {
            this._sinks = sinks;
            this.metric = new Lazy<Metric>(() => { return new Metric(name, sinks); });
        }

        public void Track(double value)
        {
            metric.Value.TrackValue(value);
        }

    }
}
