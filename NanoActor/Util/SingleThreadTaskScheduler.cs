using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NanoActor.Util
{
    public class SingleThreadTaskScheduler : TaskScheduler,IDisposable
    {
        /// <summary>Whether the current thread is processing work items.</summary>        
        private bool _threadRunning;
                
        private Thread _runningThread;

        ManualResetEvent _event = new ManualResetEvent(false);

        BlockingCollection<Task> _tasks;

        public SingleThreadTaskScheduler()
        {
            _tasks = new BlockingCollection<Task>();

            _runningThread = new Thread(() => { ThreadRun(); });
            _runningThread.IsBackground = true;
            _runningThread.Start();
            
            
        }

        protected void ThreadRun()
        {
            while (true)
            {
                _threadRunning = false;
                var task = _tasks.Take();
                _threadRunning = true;

                TryExecuteTask(task);
            }
        }

        /// <summary>Gets an enumerable of the tasks currently scheduled on this scheduler.</summary>
        /// <returns>An enumerable of the tasks currently scheduled.</returns>
        protected sealed override IEnumerable<Task> GetScheduledTasks()
        {
            // Serialize the contents of the blocking collection of tasks for the debugger
            return _tasks.ToArray();
        }



        protected override void QueueTask(Task task)
        {

            _tasks.Add(task);
            

        }

        //TODO find a better way for it
        public async Task<Boolean> WaitIdle()
        {
            while (true)
            {
                if (!_threadRunning)
                    return true;

                await Task.Delay(50);
            }
        }

        /// <summary>Attempts to remove a previously scheduled task from the scheduler.</summary>
        /// <param name="task">The task to be removed.</param>
        /// <returns>Whether the task could be found and removed.</returns>
        protected sealed override bool TryDequeue(Task task)
        {
            return false;
        }

        protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued)
        {
            // If this thread isn't already processing a task, we don't support inlining
            if (!_threadRunning) return false;

            // If the task was previously queued, remove it from the queue
            if (taskWasPreviouslyQueued) TryDequeue(task);


            return TryExecuteTask(task);

        }

        public void Dispose()
        {
            if(_runningThread!=null)
                _runningThread.Join();

            _runningThread = null;

            if (_tasks != null)
            {       
                _tasks = null;
            }
        }
    }
}
