using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NanoActor
{
    public class ActorTimer
    {

        TimeSpan _period;
        Int32? _runCount;
        Int32 _tickCount;

        Func<Task> _task;
        Func<Exception, Task> _onErrorAction;

        CancellationToken _ct;

        Boolean _disposed;

        Task _timerTask;

        public ActorTimer(Func<Task> task, TimeSpan period, Int32? runCount=null, CancellationToken? ct=null)
        {
            _period = period;
            _task = task;
            _runCount = runCount;
            _tickCount = 0;
            _ct = ct ?? CancellationToken.None;
        }

        public void Stop()
        {
            _disposed = true;
            
        }

        public void OnError(Func<Exception,Task> action)
        {
            _onErrorAction = action;
        }

        public void Start()
        {
            _disposed = false;

            _timerTask = Task.Run(async () => {

                while (true)
                {
                    if (_ct.IsCancellationRequested || _disposed)
                        break;
                   

                    await Task.Delay(_period);

                    Exception executionException = null;
                    try
                    {
                        await _task();
                    }
                    catch(Exception ex)
                    {
                        executionException = ex;
                       
                    }

                    if (executionException != null)
                        _onErrorAction(executionException).ConfigureAwait(false);


                    _tickCount++;

                    if (_runCount.HasValue && _tickCount >= _runCount.Value)
                        break;
                }

            });


        }

    }
}
