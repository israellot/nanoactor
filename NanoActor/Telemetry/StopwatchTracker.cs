using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.Telemetry
{
    public abstract class StopwatchTracker:IDisposable
    {
        private Stopwatch sw;
        private DateTimeOffset startTimestamp;


        public StopwatchTracker()
        {



        }
        
        public StopwatchTracker Start()
        {
            startTimestamp = DateTimeOffset.Now;
            sw = Stopwatch.StartNew();

            return this;
        }

        public void End(bool success=true)
        {
            sw.Stop();
            Sink(startTimestamp, sw.Elapsed, success);

            
        }

        protected abstract void Sink(DateTimeOffset startTimestamp, TimeSpan elapsed, bool success);

        public void Track(Action action)
        {

            Start();
            try
            {
                action.Invoke();

                End(true);
               
            }
            catch
            {
                try
                {
                    End(false);
                }
                catch { }

                throw;
            }

        }

        public T Track<T>(Func<T> action)
        {
            Start();
            try
            {
                var result = action.Invoke();

                End(true);
                
                return result;
            }
            catch
            {
                try
                {
                    End(false);
                }
                catch { }

                throw;
            }
        }

        public async Task Track(Func<Task> action)
        {

            Start();
            try
            {
                await action.Invoke();

                End(true);
               

            }
            catch
            {
                try
                {
                    End(false);
                }
                catch { }

                throw;
            }

        }

        public async Task<T> Track<T>(Func<Task<T>> action)
        {

            Start();
            try
            {
                var result = await action.Invoke();

                End(true);
               
                return result;
            }
            catch
            {
                try
                {
                    End(false);
                }
                catch { }

                throw;
            }

        }

        public void Dispose()
        {
            this.End(true);           
        }
    }
}
