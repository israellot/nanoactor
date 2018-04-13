using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Reflection;
using System.Threading;
using NanoActor.Util;
using NanoActor.PubSub;
using NanoActor.ActorProxy;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using NanoActor.Options;
using Microsoft.Extensions.Options;
using NanoActor.Telemetry;
using System.Diagnostics;

namespace NanoActor
{

    
    
    
    public abstract class Actor:IActor,IDisposable
    {
        
        PubSubManager _pubsub;
                
        protected static readonly ConcurrentDictionary<string, MethodInfo> _methodCache = new ConcurrentDictionary<string, MethodInfo>();
        protected static readonly ConcurrentDictionary<string, PropertyInfo> _returnPropertyCache = new ConcurrentDictionary<string, PropertyInfo>();

        CancellationTokenSource _timersCts = new CancellationTokenSource();

        List<ActorTimer> _timers = new List<ActorTimer>();

        public String Id { get; set; }

        protected ProxyFactory ProxyFactory;

        String _activatorInterface;

        IServiceProvider _serviceProvider;

        AsyncLock _executionLock = new AsyncLock();

        NanoServiceOptions _options;

        ITelemetry _telemetry;

        String _typeName;

        public event EventHandler DeactivateRequested;

        public Actor(IServiceProvider _serviceProvider)
        {
            this._serviceProvider = _serviceProvider;

            var options = _serviceProvider.GetService<IOptions<NanoServiceOptions>>();

            _telemetry = _serviceProvider.GetService<ITelemetry>();

            _options = options.Value ?? new NanoServiceOptions();

            _typeName = this.GetType().Name;
        }

        protected virtual void DeactivateRequest()
        {
            EventArgs e = new EventArgs();

            EventHandler handler = DeactivateRequested;
            if (handler != null)
            {
                handler(this, e);
            }
        }

        protected ActorTimer RegisterTimer(Func<Task> callback,TimeSpan interval,int? runCount=null,Boolean autoStart=true,Boolean sync=true)
        {
            Func<Task> innerCallback;
            if (sync)
            {
                innerCallback = () => { return QueueExecuteInline(callback); };
            }
            else
            {
                innerCallback = () => { return ExecuteInline(callback); };
            }            

            var timer = new ActorTimer(innerCallback, interval, runCount, _timersCts.Token);
            _timers.Add(timer);

            timer.OnFinish(async () => { _timers.Remove(timer); });

            timer.OnError(async (ex) => { _telemetry.Exception(ex);  });

            if (autoStart) timer.Start();

            return timer;

        }

        protected void CancelTimer(ActorTimer timer)
        {
            _timers.Remove(timer);
            timer.Stop();
        }

        protected abstract Task OnAcvitate();

        protected abstract Task SaveState();

        internal void Configure(string activatorInterface, PubSubManager pubsub)
        {
            ProxyFactory = _serviceProvider.GetRequiredService<ProxyFactory>();

            _activatorInterface = activatorInterface;
            _pubsub = pubsub;
        }

        protected Int32 _queueCount;

        public Int32 QueueCount { get { return _queueCount; } }

        internal async Task Run()
        {
            
            await this.OnAcvitate();

        }

        
        protected async Task ExecuteInline(Func<Task> task)
        {
            Task workTask = null;

            workTask = task.Invoke();
            await workTask;
        }

        protected async Task QueueExecuteInline(Func<Task> task)
        {
            Task workTask = null;

            Interlocked.Increment(ref _queueCount);
            {
                var asyncLock = await _executionLock.LockAsync();
                try
                {
                    workTask = task.Invoke();
                    await workTask;
                }
                finally
                {
                    if (asyncLock != null)
                        asyncLock.Dispose();
                    Interlocked.Decrement(ref _queueCount);
                }
            }
        }

        public async Task<object> Post(ITransportSerializer serializer,ActorRequest message,TimeSpan? timeout=null,CancellationToken? ct=null)
        {

            var key = $"{_typeName}.{message.ActorMethodName}";

            if (!_methodCache.TryGetValue(key, out var method))
            {
                method = this.GetType().GetMethod(message.ActorMethodName);
                                
                _methodCache.TryAdd(key, method);
            }

            //timeout attribute
            var timeoutAttribute = method.GetCustomAttribute<MethodTimeoutAttribute>();
            if (timeoutAttribute != null)
            {
                timeout = timeoutAttribute.Timeout;
            }
            else
            {
                timeout = timeout ?? TimeSpan.FromSeconds(_options.DefaultActorMethodTimeout);
            }

            var parameters = method.GetParameters();

            //wire parameters
            List<object> arguments = new List<object>();
            if (message is LocalActorRequest)
            {
                var localMessage = (LocalActorRequest)message;
                for (var i = 0; i < parameters.Length; i++)
                {
                    var parameterInfo = parameters[i];
                    if (localMessage.ArgumentObjects.Length > i)
                    {
                        arguments.Add(localMessage.ArgumentObjects[i]);
                    }
                    else
                    {
                        arguments.Add(Type.Missing);
                    }
                }
            }
            else
            {
                for (var i = 0; i < parameters.Length; i++)
                {
                    var parameterInfo = parameters[i];
                    if (message.Arguments.Count > i)
                    {
                        arguments.Add(serializer.Deserialize(parameterInfo.ParameterType, message.Arguments[i]));
                    }
                    else
                    {
                        arguments.Add(Type.Missing);
                    }
                }
            }

         

            Task workTask = null;

            var sync = method.GetCustomAttribute<AllowParallel>(true) == null;

            if (sync)
            {
                Interlocked.Increment(ref _queueCount);
                {

                    if (timeout.HasValue)
                    {
                        //var timeoutTask = Task.Delay(timeout.Value);

                        var sw = Stopwatch.StartNew();

                        //try to acquire lock
                        var asyncLock = await _executionLock.LockAsync(timeout.Value);

                        if (asyncLock == null)
                            throw new TimeoutException();

                        sw.Stop();

                        var remainingMs = timeout.Value.TotalMilliseconds - sw.Elapsed.TotalMilliseconds;

                        //try to run method
                        workTask = (Task)method.Invoke(this, arguments.ToArray());

                        var r = workTask.TimeoutAfter((int)remainingMs);

                        if (asyncLock != null)
                            asyncLock.Dispose();
                    }
                    else
                    {
                        var asyncLock = await _executionLock.LockAsync();

                        workTask = (Task)method.Invoke(this, arguments.ToArray());

                        await workTask;

                        if (asyncLock != null)
                            asyncLock.Dispose();
                        
                    }


                    Interlocked.Decrement(ref _queueCount);
                }
            }
            else
            {
                workTask = (Task)method.Invoke(this, arguments.ToArray());
            }
                        

            if (!_returnPropertyCache.TryGetValue(key, out var resultProperty))
            {
                if (method.ReturnType.IsConstructedGenericType)
                {
                    resultProperty = workTask.GetType().GetProperty("Result");

                    _returnPropertyCache.TryAdd(key, resultProperty);

                }
                else
                {
                    _returnPropertyCache.TryAdd(key, null);
                }
            }

            if (resultProperty != null)
            {
                var result = resultProperty.GetValue(workTask);
                return result;
            }
            else
            {
                return null;
            }


        }

        internal async Task WaitIdle()
        {
            while (_queueCount > 0)
                await Task.Delay(5);
        }

        protected async Task Publish<T>(string eventName, T data)
        {            
            await _pubsub.Publish<T>(_activatorInterface,eventName,Id,data);
        }

        protected async Task PublishFor<T>(string actorId,string eventName, T data)
        {
            await _pubsub.Publish<T>(_activatorInterface, eventName, actorId, data);
        }

        public void Dispose()
        {
            SaveState().Wait();

            foreach (var t in _timers)
            {
                t.Stop();
            }

            try
            {
                if (!_timersCts.IsCancellationRequested) _timersCts.Cancel();
            }
            catch { }
            
        }

        
    }


    public class MethodTimeoutAttribute:Attribute
    {
        public TimeSpan Timeout { get; private set; }

        public MethodTimeoutAttribute(int timeoutSeconds)
        {
            Timeout = TimeSpan.FromSeconds(timeoutSeconds);
        }
    }
}
