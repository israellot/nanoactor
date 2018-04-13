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

        public void TrackDependency(string dependencyName, string commandName, DateTimeOffset startTimestamp, TimeSpan elaspsed, bool success, IDictionary<string, string> properties = null)
        {

            var infoMessage = $"Telemetry Dependency: {dependencyName}:{commandName} {elaspsed.Humanize()} {(success ? "OK" : "FAIL")}";
            if (properties!=null && properties.Count > 0)
            {
                infoMessage += "\r\n\tProperties:\r\n\t";
                infoMessage += string.Join("\r\n\t", properties.Select(p => $"{p.Key} : {p.Value}"));
            }


            _logger.LogInformation(infoMessage);
        }

        public void TrackEvent(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            var infoMessage = $"Telemetry Event: {eventName}";
            
            if (properties != null && properties.Count > 0)
            {
                infoMessage += "\r\n\tProperties:\r\n\t";
                infoMessage += string.Join("\r\n\t", properties.Select(p => $"{p.Key} : {p.Value}"));
            }

            _logger.LogInformation(infoMessage);
        }

        public void TrackException(Exception ex, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            var infoMessage = $"Telemetry Exception: {ex.Message}";

            infoMessage += "\r\n\t Stack Trace" + ex.StackTrace;
          

            if (properties != null && properties.Count > 0)
            {
                infoMessage += "\r\n\r\n\tProperties:\r\n\t";
                infoMessage += string.Join("\r\n\t", properties.Select(p => $"{p.Key} : {p.Value}"));
            }

            _logger.LogInformation(infoMessage);
        }

        public void TrackMeter(string meterName, int count, TimeSpan period, DateTimeOffset startTimestamp, IDictionary<string, string> properties = null)
        {
            var ranges = new[] { 1, 60, 60 * 60 };

            var range = ranges.ToList().OrderBy(r => Math.Abs((double)period.TotalSeconds-(double)r)).First();

            var meter = count* range / (period.TotalSeconds) ;

            var rangeString = "";

            if (range == 1) rangeString = "sec";
            if(range == 60) rangeString = "min";
            if (range == 60*60) rangeString = "h";

            var infoMessage = $"Telemetry Meter: {meterName} {meter:f2}/{rangeString}";
            if (properties!=null && properties.Count > 0)
            {
                infoMessage+="\r\n\tProperties:\r\n\t";
                infoMessage+= string.Join("\r\n\t", properties.Select(p => $"{p.Key} : {p.Value}"));
            }
            

            _logger.LogInformation(infoMessage);

        }

        public void TrackMetric(string metricName, int count, double sum, double min, double max, double stdDeviation, TimeSpan period, DateTimeOffset startTimestamp, IDictionary<string, string> properties = null)
        {
            var infoMessage = $"Telemetry Metric: {metricName} Count: {count} Min: {min} Mean {sum/Math.Max(count,1):f2} Max: {max} Period: {period.Humanize()}";
            if (properties!=null && properties.Count > 0)
            {
                infoMessage += "\r\n\tProperties:\r\n\t";
                infoMessage += string.Join("\r\n\t", properties.Select(p => $"{p.Key} : {p.Value}"));
            }
            _logger.LogInformation(infoMessage);
        }
    }
}
