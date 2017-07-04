using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor.Telemetry
{

    public interface ITelemetrySink<T>: ITelemetrySink { }
    public interface ITelemetrySink
    {

        void TrackDependency(string dependencyName, string commandName, DateTimeOffset startTimestamp, TimeSpan elaspsed, bool success);

        void TrackMetric(string metricName, int count, double sum, double min, double max, double stdDeviation,TimeSpan period, DateTimeOffset startTimestamp);

        void TrackMeter(string meterName, int count, TimeSpan period, DateTimeOffset startTimestamp);
    }
}
