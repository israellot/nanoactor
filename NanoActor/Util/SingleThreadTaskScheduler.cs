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
        [ThreadStatic]
        private static bool _threadRunning;

        private LinkedList<Task> _tasks;

        private Thread _runningThread;

        ManualResetEvent _event = new ManualResetEvent(false);

        

        public SingleThreadTaskScheduler()
        {
            _tasks = new LinkedList<Task>();

            _runningThread = new Thread(() => { ThreadRun(); });
            _runningThread.IsBackground = true;
            _runningThread.Start();
            
        }

        protected void ThreadRun()
        {
            while (true)
            {
                _threadRunning = false;

                _event.WaitOne();

                _threadRunning = true;

                while (true)
                {
                    Task task;
                    lock (_tasks)
                    {
                        if (_tasks.Count == 0)
                        {
                            _event.Reset();
                            break;
                        }
                           

                        task = _tasks.First.Value;
                        _tasks.RemoveFirst();
                    }

                    TryExecuteTask(task);
                }
               
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
            // Add the task to the list of tasks to be processed.  If there aren't enough
            // delegates currently queued or running to process tasks, schedule another.
            lock (_tasks)
            {
                _tasks.AddLast(task);
                _event.Set();
            }

        }

        /// <summary>Attempts to remove a previously scheduled task from the scheduler.</summary>
        /// <param name="task">The task to be removed.</param>
        /// <returns>Whether the task could be found and removed.</returns>
        protected sealed override bool TryDequeue(Task task)
        {
            lock (_tasks) return _tasks.Remove(task);
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
