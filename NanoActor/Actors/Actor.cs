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

namespace NanoActor
{

    
    
    
    public abstract class Actor:IActor,IDisposable
    {

       

        PubSubManager _pubsub;
                
        protected static readonly ConcurrentDictionary<string, MethodInfo> _methodCache = new ConcurrentDictionary<string, MethodInfo>();
        protected static readonly ConcurrentDictionary<string, PropertyInfo> _returnPropertyCache = new ConcurrentDictionary<string, PropertyInfo>();

        CancellationTokenSource _timersCts = new CancellationTokenSource();

        List<ActorTimer> _timers = new List<ActorTimer>();

        BufferBlock<ActorRequest> _requestBuffer = new BufferBlock<ActorRequest>();

        public String Id { get; set; }

        protected ProxyFactory ProxyFactory;

        String _activatorInterface;

        IServiceProvider _serviceProvider;

        AsyncLock _executionLock = new AsyncLock();

        public Actor(IServiceProvider _serviceProvider)
        {
            this._serviceProvider = _serviceProvider;
        }

        protected ActorTimer RegisterTimer(Func<Task> callback,TimeSpan interval,int? runCount=null,Boolean autoStart=true)
        {
            var timer = new ActorTimer(callback, interval, runCount, _timersCts.Token);
            _timers.Add(timer);

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

        public async Task<object> Post(ITransportSerializer serializer,ActorRequest message,TimeSpan? timeout=null,CancellationToken? ct=null)
        {

            var key = $"{this.GetType().Name}.{message.ActorMethodName}";

            if (!_methodCache.TryGetValue(key, out var method))
            {
                method = this.GetType().GetMethod(message.ActorMethodName);

                _methodCache.TryAdd(key, method);

            }
            var parameters = method.GetParameters();

            List<object> arguments = new List<object>();
            for (var i = 0; i < message.Arguments.Count; i++)
            {
                var parameterInfo = parameters[i];
                arguments.Add(serializer.Deserialize(parameterInfo.ParameterType, message.Arguments[i]));
            }

            Task workTask = null;

            Interlocked.Increment(ref _queueCount);


            {
                var asyncLock = await _executionLock.LockAsync();
                try
                {
                    workTask = (Task)method.Invoke(this, arguments.ToArray());
                    await workTask;
                }
                finally
                {
                    if (asyncLock != null)
                        ((IDisposable)asyncLock).Dispose();
                    Interlocked.Decrement(ref _queueCount);
                }
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

            
        }

        
    }
}
