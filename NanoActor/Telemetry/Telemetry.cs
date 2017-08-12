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

        Dictionary<string, string> _properties = new Dictionary<string, string>();

        protected ITelemetrySink[] _sinks;
        public Telemetry(ITelemetrySink[] sinks)
        {
            _sinks = sinks;
        }

        public virtual DependencyTracker Dependency(String dependencyName, string commandName, IDictionary<string, string> properties = null)
        {
            properties = properties ?? new Dictionary<string, string>();
            foreach (var p in _properties)
                if (!properties.ContainsKey(p.Key))
                    properties[p.Key] = p.Value;

            return new DependencyTracker(_sinks, dependencyName, commandName, properties);
        }

        public virtual MetricTracker Metric(string metricName, IDictionary<string, string> properties = null)
        {
            properties = properties ?? new Dictionary<string, string>();
            foreach (var p in _properties)
                if (!properties.ContainsKey(p.Key))
                    properties[p.Key] = p.Value;

            var metric = new MetricTracker(_sinks, metricName, properties);

            return metric;
        }

        public virtual MeterTracker Meter(string meterName,TimeSpan? aggregationPeriod,IDictionary<string,string> properties=null)
        {
            properties = properties ?? new Dictionary<string, string>();
            foreach (var p in _properties)
                if (!properties.ContainsKey(p.Key))
                    properties[p.Key] = p.Value;

            return new MeterTracker(_sinks, meterName, aggregationPeriod, properties);
        }

        public virtual void SetProperties(IDictionary<string, string> properties)
        {


            foreach(var p in properties)
            {
                _properties[p.Key] = p.Value;
            }
        }

        public virtual void Event(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            properties = properties ?? new Dictionary<string, string>();
            foreach (var p in _properties)
                if (!properties.ContainsKey(p.Key))
                    properties[p.Key] = p.Value;

            foreach (var s in _sinks)
            {
                s.TrackEvent(eventName, properties, metrics);
            }
        }

        public virtual void Exception(Exception ex, String description = null, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            properties = properties ?? new Dictionary<string, string>();
            foreach (var p in _properties)
                if (!properties.ContainsKey(p.Key))
                    properties[p.Key] = p.Value;

            if (!string.IsNullOrEmpty(description))
                properties["Description"] = description;

            foreach (var s in _sinks)
            {
                s.TrackException(ex, properties, metrics);
            }
        }
    }

    public class Telemetry<T> : Telemetry,ITelemetry<T>
    {

        ITelemetry _telemetry;
        public Telemetry(LoggerTelemetrySink<T> logger, ITelemetrySink[] sinks, ITelemetry telemetry) : base(sinks.Concat(new[] { logger }).ToArray())
        {
            _telemetry = telemetry;

            if (_sinks != null)
            {
                _sinks = _sinks.Where(s => s.GetType() != typeof(LoggerTelemetrySink)).ToArray();
            }
                        
        }

        public override void SetProperties(IDictionary<string, string> properties)
        {
            _telemetry.SetProperties(properties);

            base.SetProperties(properties);
        }

       
    }
}
