using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Threading.Tasks;
using System.Reflection;
using System.Threading;
using NanoActor.Util;
using NanoActor.PubSub;

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

        public String Id { get; set; }

        String _activatorInterface;

        public Actor()
        {            
            
            
        }

        internal void Configure(string activatorInterface, PubSubManager pubsub)
        {
            _activatorInterface = activatorInterface;
            _pubsub = pubsub;
        }

        internal void Run()
        {
            _taskScheduler = new OrderedTaskScheduler();
        }

        public async Task<object> Post(ActorRequest message,TimeSpan? timeout=null,CancellationToken? ct=null)
        {
            if (_taskScheduler == null)
                return null;

            try
            {
                
                var taskResult = await Task.Factory.StartNew(async () => {
                    
                    if (!_methodCache.TryGetValue(message.ActorMethodName, out var method))
                    {
                        method = this.GetType().GetMethod(message.ActorMethodName);

                        _methodCache[message.ActorMethodName] = method;
                    }

                    var workTask = (Task)method.Invoke(this, message.Arguments);
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

                }, ct??CancellationToken.None,TaskCreationOptions.None,_taskScheduler);


                return taskResult.Result;
                
            }
            catch (Exception ex)
            {
                return ex;
            }

           
        }

        public Task WaitIdle()
        {
            return _taskScheduler.WaitIdle();
        }

        protected async Task Publish(string eventName, object data)
        {            
            await _pubsub.Publish(_activatorInterface,eventName,Id,data);
        }

        public void Dispose()
        {
           
        }
    }
}
