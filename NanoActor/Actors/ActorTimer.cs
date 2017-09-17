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
        Func<Task> _onFinish;

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

        public void OnFinish(Func<Task> action)
        {
            _onFinish = action;
        }

        public void Start()
        {
            _disposed = false;

            _timerTask = Task.Run(async () => {

                while (true)
                {
                                     
                    Exception executionException = null;
                    try
                    {
                        await Task.Delay(_period, _ct);

                        if (_ct.IsCancellationRequested || _disposed)
                            break;

                        await _task();
                    }
                    catch(Exception ex)
                    {
                        executionException = ex;                       
                    }

                    if (_ct.IsCancellationRequested || _disposed)
                        break;

                    if (executionException != null)
                    if (_onErrorAction != null)
                    {
                        try
                        {
                            _onErrorAction(executionException).ConfigureAwait(false);
                        }
                        catch { }
                    }


                    _tickCount++;

                    if (_runCount.HasValue && _tickCount >= _runCount.Value)
                        break;
                }

                _onFinish?.Invoke();

            });


        }

    }
}
