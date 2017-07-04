﻿using Castle.DynamicProxy;
using NanoActor.Telemetry;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.ActorProxy
{
    public class RemoteActorMethodInterceptor : IAsyncInterceptor
    {

        RemoteStageClient _remoteClient;

        Boolean _fireAndForget;

        TimeSpan _timeout;

        ITransportSerializer _serializer;

        ITelemetry _telemetry;

        protected static ConcurrentDictionary<string, Func<byte[],object>> _deserializerAccessors = new ConcurrentDictionary<string, Func<byte[], object>>();

        public RemoteActorMethodInterceptor(RemoteStageClient remoteClient, ITelemetry telemetry, ITransportSerializer serializer,TimeSpan? timeout=null, Boolean fireAndForget=false)
        {
            _remoteClient = remoteClient;
            _fireAndForget = fireAndForget;
            _serializer = serializer;
            this._telemetry = telemetry;
#if DEBUG
            _timeout = TimeSpan.FromMilliseconds(-1);
#else
            _timeout = timeout ?? TimeSpan.FromSeconds(5);
#endif

        }

        public void InterceptAsynchronous(IInvocation invocation)
        {

            var actor = (IActor)invocation.Proxy;

            List<byte[]> arguments = new List<byte[]>();
            foreach (var argument in invocation.Arguments)
                arguments.Add(_serializer.Serialize(argument));

            var message = new ActorRequest()
            {
                ActorId = actor.Id,
                ActorInterface = $"{invocation.Method.DeclaringType.Namespace}.{invocation.Method.DeclaringType.Name}",
                ActorMethodName = invocation.Method.Name,
                Arguments = arguments,
                FireAndForget=_fireAndForget
            };

                        
            var task = Task.Run(() =>{

                var tracker = _telemetry.Dependency($"proxy: {message.ActorInterface}", message.ActorMethodName).Start();

                return _remoteClient.SendActorRequest(message, _timeout)
                .ContinueWith((t) =>
                {
                    
                    if (t.IsFaulted)
                    {
                        tracker.End(false);
                        if (t.Exception != null)
                        {
                            throw t.Exception;
                        }
                    }

                    var result = t.Result;
                    if (!result.Success)
                    {
                        tracker.End(false);
                        if (result.Exception != null)
                            throw result.Exception;
                    }

                    tracker.End(true);
                });
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

            List<byte[]> arguments = new List<byte[]>();
            foreach(var argument in invocation.Arguments)
                arguments.Add(_serializer.Serialize(argument));
            

            var message = new ActorRequest()
            {
                ActorId = actor.Id,
                ActorInterface = $"{invocation.Method.DeclaringType.Namespace}.{invocation.Method.DeclaringType.Name}",
                ActorMethodName = invocation.Method.Name,
                Arguments = arguments,
                FireAndForget = _fireAndForget
            };

            var task = Task.Run(() =>
            {
                var tracker = _telemetry.Dependency($"proxy: {message.ActorInterface}", message.ActorMethodName).Start();

                return _remoteClient.SendActorRequest(message, _timeout)
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
                            tracker.End(false);
                            if (result.Exception != null)
                                throw result.Exception;
                        }

                        var key = string.Join(".", invocation.Method.DeclaringType.Namespace, invocation.Method.DeclaringType.Name, invocation.Method.Name);

                        if (!_deserializerAccessors.TryGetValue(key, out var method))
                        {
                            var returnType = invocation.Method.ReturnType.GetGenericArguments()[0];

                            var methodInfo = _serializer.GetType().GetMethod("Deserialize").MakeGenericMethod(returnType);

                            method = (x) => methodInfo.Invoke(_serializer, new[] { x });

                            _deserializerAccessors.TryAdd(key, method);
                        }
                                                
                        var obj = method.Invoke(result.Response);

                        tracker.End(true);

                        return (TResult)(obj);
                    }

                });
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
