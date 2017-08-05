using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NanoActor.Telemetry
{

    public class MeterDataPoint
    {
        public DateTimeOffset StartTimestamp { get; set; }

        public int Count { get; set; }

        public TimeSpan Period { get; set; }


        public double PerSecond { get
            {
                return Count / Period.TotalSeconds;
            }
        }

        public double PerMinute
        {
            get
            {
                return Count *60 / Period.TotalSeconds;
            }
        }

        public double PerHour
        {
            get
            {
                return Count * 60 * 60 / Period.TotalSeconds;
            }
        }

        public MeterDataPoint() { }

        public MeterDataPoint(MeterAggregator agg,TimeSpan period)
        {
            this.Count = agg.Count;
            this.StartTimestamp = agg.StartTimestamp;
            this.Period = period;
        }
    }

    /// <summary>
    /// Aggregates metric values for a single time period.
    /// </summary>
    public class MeterAggregator
    {
        
        public DateTimeOffset StartTimestamp { get; }
                
        volatile int _count = 0;

        public int Count { get { return _count; } }

        public MeterAggregator(DateTimeOffset startTimestamp)
        {
           
            this.StartTimestamp = startTimestamp;
        }

        public void Tick()
        {
            Interlocked.Increment(ref _count);
        }
    }   // internal class MetricAggregator

    public enum MeterUnit
    {
        Second,
        Minute,
        Hour
    }

    /// <summary>
    /// Accepts metric values and sends the aggregated values at 1-minute intervals.
    /// </summary>
    /// 
    public sealed class Meter : IDisposable
    {
        private TimeSpan _aggregationPeriod;

        Int32 _historyCount;

        private bool _isDisposed = false;
        private MeterAggregator _aggregator = null;
        private readonly ITelemetrySink[] _sinks;

        IDictionary<string, string> _properties;

        public string Name { get; }

        public LinkedList<MeterDataPoint> History { get; private set; } = new LinkedList<MeterDataPoint>();

        public Meter(string name, ITelemetrySink[] sinks, TimeSpan? aggregationPeriod = null,Int32 historyKeep=30, IDictionary<string, string> properties=null)
        {
            
            if (aggregationPeriod == TimeSpan.Zero)
                throw new ArgumentException("Aggregation Period can't be zero", "aggregationPeriod");

          
            _aggregationPeriod = aggregationPeriod?? TimeSpan.FromSeconds(60);

            this.Name = name ?? "null";
            this._aggregator = new MeterAggregator(DateTimeOffset.UtcNow);
            this._sinks = sinks ?? throw new ArgumentNullException(nameof(sinks));
            this._historyCount = historyKeep;
            this._properties = properties;



            Task.Run(this.AggregatorLoopAsync);
        }

        public void Tick()
        {
            MeterAggregator currAggregator = _aggregator;
            if (currAggregator != null)
            {
                currAggregator.Tick();
            }
        }

        private async Task AggregatorLoopAsync()
        {
            while (_isDisposed == false)
            {
                try
                {
                    // Wait for end end of the aggregation period:
                    await Task.Delay(_aggregationPeriod).ConfigureAwait(continueOnCapturedContext: false);

                    // Atomically snap the current aggregation:
                    MeterAggregator nextAggregator = new MeterAggregator(DateTimeOffset.UtcNow);
                    MeterAggregator prevAggregator = Interlocked.Exchange(ref _aggregator, nextAggregator);

                    // Compute the actual aggregation period length:
                    TimeSpan aggPeriod = nextAggregator.StartTimestamp - prevAggregator.StartTimestamp;

                    History.AddFirst(new MeterDataPoint(prevAggregator, aggPeriod));
                    if (History.Count > _historyCount)
                    {
                        for(var i =0;i< History.Count - _historyCount;i++)
                            History.RemoveLast();
                    }
                       

                    // Only send anything if at least one value was measured:
                    if (prevAggregator != null)
                    {                        
                        if (aggPeriod.TotalMilliseconds >0)
                        {
                            foreach (var sink in _sinks)
                            {
                                try
                                {
                                    sink.TrackMeter(
                                   Name,
                                   prevAggregator.Count,
                                   aggPeriod,
                                   prevAggregator.StartTimestamp,
                                   _properties
                                   );
                                }catch(Exception ex)
                                {

                                }
                                
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
