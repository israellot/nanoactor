using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Threading.Tasks;
using System.Reflection;
using System.Threading;

namespace NanoActor
{
    
    
    public abstract class QueueActor:IActor
    {

        IActorMessageQueue _messageQueue;

        Task _processTask;

        CancellationTokenSource _cts;

        public String Id { get; set; }

        public QueueActor()
        {
            _cts = new CancellationTokenSource();

            _messageQueue = new LocalActorMessageQueue();
            
        }

        public void Run()
        {
            _processTask = ProcessTask(_cts.Token);

            _processTask.ConfigureAwait(false);
        }

        public async Task<object> Post(ActorRequest message,TimeSpan? timeout=null,CancellationToken? ct=null)
        {
            var response = await _messageQueue.EnqueueAndWaitResponse(message, timeout,ct??_cts.Token);

            return response;
        }

        protected Dictionary<string, MethodInfo> _methodCache = new Dictionary<string, MethodInfo>();
        protected Dictionary<string, PropertyInfo> _returnPropertyCache = new Dictionary<string, PropertyInfo>();

        public Task ProcessTask(CancellationToken ct)
        {
            return Task.Run(async () =>
            {
                while (true)
                {
                    var message = await _messageQueue.Dequeue(ct);

                    if (message != null)
                    {
                        try
                        {
                            if(!_methodCache.TryGetValue(message.ActorMethodName,out var method))
                            {
                                method = this.GetType().GetMethod(message.ActorMethodName);

                                _methodCache[message.ActorMethodName] = method;
                            }

                            var workTask = (Task)method.Invoke(this, message.Arguments);

                            await workTask;

                            if(!_returnPropertyCache.TryGetValue(message.ActorMethodName,out var resultProperty))
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

                                _messageQueue.EnqueueResponse(message, result);
                            }
                            else
                            {
                                _messageQueue.EnqueueResponse(message, null);
                            }
                            
                        }catch(Exception ex)
                        {
                            _messageQueue.EnqueueResponse(message, ex);
                        }
                    }
                    else
                    {
                        if (ct.IsCancellationRequested)
                            break;
                    }
                }
                
            });
        }

    }
}
