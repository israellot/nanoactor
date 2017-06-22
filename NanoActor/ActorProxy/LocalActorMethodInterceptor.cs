using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.ActorProxy
{
    public class LocalActorMethodInterceptor : IAsyncInterceptor
    {

        LocalStage _localStage;
        public LocalActorMethodInterceptor(LocalStage localStage)
        {
            _localStage = localStage;
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

                var result = await _localStage.Execute(message);

                if (result as Exception != null)
                {
                    throw (Exception)result;
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

                var result = await _localStage.Execute(message);

                if (result as Exception != null)
                {
                    throw (Exception)result;
                }

                return (TResult)result;

            });

        }

        public void InterceptSynchronous(IInvocation invocation)
        {
            invocation.Proceed();
        }


    }
}
