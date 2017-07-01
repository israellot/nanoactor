using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor.Telemetry
{
    public class DependencyTracker : StopwatchTracker
    {
        protected string dependencyName;
        protected string commandName;
        ITelemetrySink[] _sinks;

        public DependencyTracker(ITelemetrySink[] sinks, String dependencyName, string commandName)
        {
            this.dependencyName = dependencyName;
            this.commandName = commandName;
            this._sinks = sinks;
        }

        protected override void Sink(DateTimeOffset startTimestamp, TimeSpan elapsed, bool success)
        {
            foreach (var sink in _sinks)
            {
                sink.TrackDependency(dependencyName, commandName, startTimestamp, elapsed, success);
            }
        }
    }
}
