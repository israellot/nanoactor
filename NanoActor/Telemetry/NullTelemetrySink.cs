﻿using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor.Telemetry
{
    public class NullTelemetrySink : ITelemetrySink
    {
        public void TrackDependency(string dependencyName, string commandName, DateTimeOffset startTimestamp, TimeSpan elaspsed, bool success, IDictionary<string, string> properties = null)
        {
           
        }

        public void TrackMeter(string meterName, int count, TimeSpan period, DateTimeOffset startTimestamp, IDictionary<string, string> properties = null)
        {
           
        }

        public void TrackMetric(string metricName, int count, double sum, double min, double max, double stdDeviation, TimeSpan period, DateTimeOffset startTimestamp, IDictionary<string, string> properties = null)
        {
            
        }

        public void TrackEvent(string eventName, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
            
        }

        public void TrackException(Exception ex, IDictionary<string, string> properties = null, IDictionary<string, double> metrics = null)
        {
          
        }
    }
}
