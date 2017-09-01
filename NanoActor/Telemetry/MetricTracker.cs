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

        IDictionary<string, string> _properties;

        String _name;

        public MetricTracker(ITelemetrySink[] sinks,string name,IDictionary<string,string> properties=null)
        {
            _sinks = sinks;
            _properties = properties;
            _name = name;
            this.metric = new Lazy<Metric>(() => { return new Metric(_name, _sinks, _properties); });
        }

        public void Track(double value)
        {
            metric.Value.TrackValue(value);
        }

    }
}
