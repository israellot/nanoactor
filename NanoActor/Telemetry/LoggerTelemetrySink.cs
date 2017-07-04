using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;
using Humanizer;
using System.Linq;

namespace NanoActor.Telemetry
{

    public class LoggerTelemetrySink<T> : LoggerTelemetrySink,ITelemetrySink<T>
    {
        public LoggerTelemetrySink(ILogger<T> logger) : base(logger)
        {
            
        }
    }

    public class LoggerTelemetrySink: ITelemetrySink
    {

        ILogger _logger;

        public LoggerTelemetrySink(ILogger logger)
        {
            _logger = logger;
        }

        public void TrackDependency(string dependencyName, string commandName, DateTimeOffset startTimestamp, TimeSpan elaspsed, bool success)
        {
            _logger.LogInformation($"Telemetry Dependency: {dependencyName}:{commandName} {elaspsed.Humanize()} {(success?"OK":"FAIL")}");
        }

        public void TrackMeter(string meterName, int count, TimeSpan period, DateTimeOffset startTimestamp)
        {
            var ranges = new[] { 1, 60, 60 * 60 };

            var range = ranges.ToList().OrderBy(r => Math.Abs((double)period.TotalSeconds-(double)r)).First();

            var meter = count* range / (period.TotalSeconds) ;

            var rangeString = "";

            if (range == 1) rangeString = "sec";
            if(range == 60) rangeString = "min";
            if (range == 60*60) rangeString = "h";

            _logger.LogInformation($"Telemetry Meter: {meterName} {meter:f2}/{rangeString}");

        }

        public void TrackMetric(string metricName, int count, double sum, double min, double max, double stdDeviation, TimeSpan period, DateTimeOffset startTimestamp)
        {
            _logger.LogInformation($"Telemetry Metric: {metricName} Count: {count} Min: {min} Max: {max} Period: {period.Humanize()}");
        }
    }
}
