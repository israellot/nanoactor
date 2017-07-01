using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.Telemetry
{
    public abstract class StopwatchTracker
    {
        private Stopwatch sw;
        private DateTimeOffset startTimestamp;


        public StopwatchTracker()
        {



        }


        protected void Start()
        {
            startTimestamp = DateTimeOffset.Now;
            sw = Stopwatch.StartNew();
        }

        protected void End()
        {
            sw.Stop();
        }

        protected abstract void Sink(DateTimeOffset startTimestamp, TimeSpan elapsed, bool success);

        public void Track(Action action)
        {

            Start();
            try
            {
                action.Invoke();

                End();
                Sink(startTimestamp, sw.Elapsed, true);
            }
            catch
            {
                try
                {
                    Sink(startTimestamp, sw.Elapsed, false);
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

                End();
                Sink(startTimestamp, sw.Elapsed, true);
                return result;
            }
            catch
            {
                try
                {
                    Sink(startTimestamp, sw.Elapsed, false);
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

                End();
                Sink(startTimestamp, sw.Elapsed, true);

            }
            catch
            {
                try
                {
                    Sink(startTimestamp, sw.Elapsed, false);
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

                End();
                Sink(startTimestamp, sw.Elapsed, true);
                return result;
            }
            catch
            {
                try
                {
                    Sink(startTimestamp, sw.Elapsed, false);
                }
                catch { }

                throw;
            }

        }

    }
}
