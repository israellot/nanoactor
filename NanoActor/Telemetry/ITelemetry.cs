using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor.Telemetry
{
    public interface ITelemetry<T> : ITelemetry { }

    public interface ITelemetry
    {
        DependencyTracker Dependency(String dependencyName, string commandName, IDictionary<string, string> properties = null);
        MetricTracker Metric(string metricName, IDictionary<string, string> properties = null);
        MeterTracker Meter(string meterName, TimeSpan? aggregationPeriod, IDictionary<string, string> properties = null);

        void Event(string eventName, IDictionary<string, string> properties=null, IDictionary<string, double> metrics=null);

        void Exception(Exception ex, String description=null, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null);

        void SetProperties(IDictionary<string, string> properties);
    }
}
