using MoreLinq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NanoActor.Telemetry
{
    public class Telemetry: ITelemetry
    {

        static ConcurrentDictionary<string, MetricTracker> metricDictionary = new ConcurrentDictionary<string, MetricTracker>();

        static ConcurrentDictionary<string, MeterTracker> meterDictionary = new ConcurrentDictionary<string, MeterTracker>();

        protected ITelemetrySink[] _sinks;
        public Telemetry(ITelemetrySink[] sinks)
        {
            _sinks = sinks;
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

    public class Telemetry<T> : Telemetry,ITelemetry<T>
    {
        public Telemetry(LoggerTelemetrySink<T> logger, ITelemetrySink[] sinks) : base(sinks.Concat(new[] { logger }).ToArray())
        {
            if (_sinks != null)
            {
                _sinks = _sinks.Where(s => s.GetType() != typeof(LoggerTelemetrySink)).ToArray();
            }
          

        }
    }
}
