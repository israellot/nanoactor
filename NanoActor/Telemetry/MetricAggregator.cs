using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NanoActor.Telemetry
{
    /// <summary>
    /// Aggregates metric values for a single time period.
    /// </summary>
    internal class MetricAggregator
    {
        private SpinLock _trackLock = new SpinLock();

        public DateTimeOffset StartTimestamp { get; }
        public int Count { get; private set; }
        public double Sum { get; private set; }
        public double SumOfSquares { get; private set; }
        public double Min { get; private set; }
        public double Max { get; private set; }
        public double Average { get { return (Count == 0) ? 0 : (Sum / Count); } }
        public double Variance
        {
            get
            {
                return (Count == 0) ? 0 : (SumOfSquares / Count)
                                          - (Average * Average);
            }
        }
        public double StandardDeviation { get { return Math.Sqrt(Variance); } }

        public MetricAggregator(DateTimeOffset startTimestamp)
        {
            this.StartTimestamp = startTimestamp;
        }

        public void TrackValue(double value)
        {
            bool lockAcquired = false;

            try
            {
                _trackLock.Enter(ref lockAcquired);

                if ((Count == 0) || (value < Min)) { Min = value; }
                if ((Count == 0) || (value > Max)) { Max = value; }
                Count++;
                Sum += value;
                SumOfSquares += value * value;
            }
            finally
            {
                if (lockAcquired)
                {
                    _trackLock.Exit();
                }
            }
        }
    }   // internal class MetricAggregator

    /// <summary>
    /// Accepts metric values and sends the aggregated values at 1-minute intervals.
    /// </summary>
    public sealed class Metric : IDisposable
    {
        private static readonly TimeSpan AggregationPeriod = TimeSpan.FromSeconds(60);

        private bool _isDisposed = false;
        private MetricAggregator _aggregator = null;
        private readonly ITelemetrySink[] _sinks;

        private IDictionary<string, string> _properties;

        public string Name { get; }

        public Metric(string name, ITelemetrySink[] sinks,IDictionary<string,string> properties=null)
        {
            this.Name = name ?? "null";
            this._aggregator = new MetricAggregator(DateTimeOffset.UtcNow);
            this._sinks = sinks ?? throw new ArgumentNullException(nameof(sinks));
            _properties = properties;

            Task.Run(this.AggregatorLoopAsync);
        }

        public void TrackValue(double value)
        {
            MetricAggregator currAggregator = _aggregator;
            if (currAggregator != null)
            {
                currAggregator.TrackValue(value);
            }
        }

        private async Task AggregatorLoopAsync()
        {
            while (_isDisposed == false)
            {
                try
                {
                    // Wait for end end of the aggregation period:
                    await Task.Delay(AggregationPeriod).ConfigureAwait(continueOnCapturedContext: false);

                    // Atomically snap the current aggregation:
                    MetricAggregator nextAggregator = new MetricAggregator(DateTimeOffset.UtcNow);
                    MetricAggregator prevAggregator = Interlocked.Exchange(ref _aggregator, nextAggregator);
                    
                    // Only send anything is at least one value was measured:
                    if (prevAggregator != null && prevAggregator.Count > 0)
                    {
                        // Compute the actual aggregation period length:
                        TimeSpan aggPeriod = nextAggregator.StartTimestamp - prevAggregator.StartTimestamp;
                        if (aggPeriod.TotalMilliseconds < 1)
                        {
                            aggPeriod = TimeSpan.FromMilliseconds(1);
                        }

                        foreach (var sink in _sinks)
                        {
                            try
                            {
                                sink.TrackMetric(
                                 Name,
                                 prevAggregator.Count,
                                 prevAggregator.Sum,
                                 prevAggregator.Min,
                                 prevAggregator.Max,
                                 prevAggregator.StandardDeviation,
                                 aggPeriod,
                                 prevAggregator.StartTimestamp,
                                 _properties
                                 );
                            }catch(Exception ex) {

                            }
                          
                        }

                       

                        
                    }
                }
                catch (Exception ex)
                {
                   
                }
            }
        }

        void IDisposable.Dispose()
        {
            _isDisposed = true;
            _aggregator = null;
        }
    }   // public sealed class Metric
}
