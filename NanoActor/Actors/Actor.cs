﻿using System;
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

namespace NanoActor
{

    public class ActorEvent<T>
    {
        public String ActorInterface { get; set; }

        public String EventName { get; set; }

        public String ActorId { get; set; }

        public T EventData { get; set; }
    }
    
    
    public abstract class Actor:IActor,IDisposable
    {

        OrderedTaskScheduler _taskScheduler;

        PubSubManager _pubsub;
                
        protected static readonly Dictionary<string, MethodInfo> _methodCache = new Dictionary<string, MethodInfo>();
        protected static readonly Dictionary<string, PropertyInfo> _returnPropertyCache = new Dictionary<string, PropertyInfo>();

        CancellationTokenSource _timersCts = new CancellationTokenSource();

        List<ActorTimer> _timers = new List<ActorTimer>();

        public String Id { get; set; }

        protected ProxyFactory ProxyFactory;

        String _activatorInterface;

        IServiceProvider _serviceProvider;


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

        internal async Task Run()
        {
            _taskScheduler = new OrderedTaskScheduler();

            await this.OnAcvitate();

        }

        public async Task<object> Post(ITransportSerializer serializer,ActorRequest message,TimeSpan? timeout=null,CancellationToken? ct=null)
        {
            if (_taskScheduler == null)
                return null;

            try
            {

                var taskResult = await Task.Factory.StartNew(async () =>
                {

                    if (!_methodCache.TryGetValue(message.ActorMethodName, out var method))
                    {
                        method = this.GetType().GetMethod(message.ActorMethodName);

                        _methodCache[message.ActorMethodName] = method;
                    }
                    var parameters = method.GetParameters();

                    List <object> arguments = new List<object>();
                    for (var i = 0; i < message.Arguments.Count; i++)
                    {
                        var parameterInfo = parameters[i];                                             
                        arguments.Add(serializer.Deserialize(parameterInfo.ParameterType, message.Arguments[i]));
                    }

                    Task workTask = (Task)method.Invoke(this, arguments.ToArray());
                    await workTask;


                    if (!_returnPropertyCache.TryGetValue(message.ActorMethodName, out var resultProperty))
                    {
                        if (method.ReturnType.IsConstructedGenericType)
                        {
                            resultProperty = workTask.GetType().GetProperty("Result");
                            _returnPropertyCache[message.ActorMethodName] = resultProperty;
                        }
                        else
                        {
                            _returnPropertyCache[message.ActorMethodName] = null;
                        }

                    }

                    if (resultProperty != null)
                    {
                        var result = resultProperty.GetValue(workTask);

                        return result;
                    }

                    return null;

                }, ct ?? CancellationToken.None, TaskCreationOptions.None, _taskScheduler).ConfigureAwait(true);


                return taskResult.Result;
                
            }
            catch (Exception ex)
            {
                if (ex.InnerException != null)
                    return ex.InnerException;
                else
                    return ex;
            }

           
        }

        internal Task WaitIdle()
        {
            return _taskScheduler.WaitIdle();
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
            foreach(var t in _timers)
            {
                t.Stop();
            }

            SaveState().Wait();
        }
    }
}
