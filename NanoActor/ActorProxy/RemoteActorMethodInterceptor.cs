using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.ActorProxy
{
    public class RemoteActorMethodInterceptor : IAsyncInterceptor
    {

        RemoteStage _remoteStage;

        public RemoteActorMethodInterceptor(RemoteStage remoteStage)
        {
            _remoteStage = remoteStage;
        }

        public void InterceptAsynchronous(IInvocation invocation)
        {
            invocation.ReturnValue = Task.Run(async () => {

                var actor = (IActor)invocation.Proxy;
                var message = new ActorRequest()
                {
                    ActorId = actor.Id,
                    ActorInterface = $"{invocation.Method.DeclaringType.Namespace}.{invocation.Method.DeclaringType.Name}",
                    ActorMethodName = invocation.Method.Name,
                    Arguments = invocation.Arguments
                };

                var result = await _remoteStage.SendActorRequest(message);

                if (!result.Success)
                {
                    if (result.Exception != null)
                        throw result.Exception;
                }


            });
        }

        public void InterceptAsynchronous<TResult>(IInvocation invocation)
        {

            invocation.ReturnValue = Task.Run<TResult>(async () => {

                var actor = (IActor)invocation.Proxy;
                var message = new ActorRequest()
                {
                    ActorId = actor.Id,
                    ActorInterface = $"{invocation.Method.DeclaringType.Namespace}.{invocation.Method.DeclaringType.Name}",
                    ActorMethodName = invocation.Method.Name,
                    Arguments = invocation.Arguments
                };

                var result = await _remoteStage.SendActorRequest(message);

                if (!result.Success)
                {
                    if (result.Exception != null)
                        throw result.Exception;
                }

                return (TResult)((dynamic)result.Response);

            });

        }

        public void InterceptSynchronous(IInvocation invocation)
        {
            invocation.Proceed();
        }


    }
}
