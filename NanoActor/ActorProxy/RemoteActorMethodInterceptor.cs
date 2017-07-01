using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.ActorProxy
{
    public class RemoteActorMethodInterceptor : IAsyncInterceptor
    {

        RemoteStageClient _remoteClient;

        Boolean _fireAndForget;

        TimeSpan _timeout;

        public RemoteActorMethodInterceptor(RemoteStageClient remoteClient,TimeSpan? timeout=null, Boolean fireAndForget=false)
        {
            _remoteClient = remoteClient;
            _fireAndForget = fireAndForget;
            _timeout = timeout ?? TimeSpan.FromSeconds(5);
        }

        public void InterceptAsynchronous(IInvocation invocation)
        {

            var actor = (IActor)invocation.Proxy;
            var message = new ActorRequest()
            {
                ActorId = actor.Id,
                ActorInterface = $"{invocation.Method.DeclaringType.Namespace}.{invocation.Method.DeclaringType.Name}",
                ActorMethodName = invocation.Method.Name,
                Arguments = invocation.Arguments,
                FireAndForget=_fireAndForget
            };


            var task = _remoteClient.SendActorRequest(message, _timeout)
                .ContinueWith((t) => {

                    if (t.IsFaulted)
                    {
                        if (t.Exception != null)
                        {
                            throw t.Exception;
                        }
                    }

                    var result = t.Result;
                    if (!result.Success)
                    {
                        if (result.Exception != null)
                            throw result.Exception;
                    }
                });

            if (_fireAndForget)
            {
                invocation.ReturnValue = Task.CompletedTask;
            }
            else
            {
                invocation.ReturnValue = task;
            }

           
        }

        public void InterceptAsynchronous<TResult>(IInvocation invocation)
        {

            var actor = (IActor)invocation.Proxy;
            var message = new ActorRequest()
            {
                ActorId = actor.Id,
                ActorInterface = $"{invocation.Method.DeclaringType.Namespace}.{invocation.Method.DeclaringType.Name}",
                ActorMethodName = invocation.Method.Name,
                Arguments = invocation.Arguments,
                FireAndForget = _fireAndForget
            };

            var task= _remoteClient.SendActorRequest(message, _timeout)
                .ContinueWith(t =>
                {
                    if (_fireAndForget)
                    {
                        return default(TResult);
                    }
                    else
                    {
                        var result = t.Result;

                        if (!result.Success)
                        {
                            if (result.Exception != null)
                                throw result.Exception;
                        }

                        return (TResult)((dynamic)result.Response);
                    }
                    
                });

            if (_fireAndForget)
            {
                invocation.ReturnValue = Task.FromResult<TResult>(default(TResult));
            }
            else
            {
                invocation.ReturnValue = task;
            }
           

            

        }

        public void InterceptSynchronous(IInvocation invocation)
        {
            invocation.Proceed();
        }


    }
}
