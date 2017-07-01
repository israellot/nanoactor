using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;

namespace NanoActor.Telemetry
{
    public class Telemetry: ITelemetry
    {

        static ConcurrentDictionary<string, MetricTracker> metricDictionary = new ConcurrentDictionary<string, MetricTracker>();

        static ConcurrentDictionary<string, MeterTracker> meterDictionary = new ConcurrentDictionary<string, MeterTracker>();

        ITelemetrySink[] _sinks;
        public Telemetry(ITelemetrySink[] sinks)
        {
            this._sinks = sinks;
        }

        public DependencyTracker Dependency(String dependencyName, string commandName)
        {
            return new DependencyTracker(_sinks, dependencyName, commandName);
        }

        public MetricTracker Metric(string metricName)
        {
            var metric = metricDictionary.GetOrAdd(metricName, new MetricTracker(_sinks, metricName));

            return metric;
        }

        public MeterTracker Meter(string meterName,TimeSpan? aggregationPeriod)
        {
            return meterDictionary.GetOrAdd(meterName, new MeterTracker(_sinks, meterName, aggregationPeriod));
        }

    }
}
