﻿using Castle.DynamicProxy;
using Microsoft.Extensions.Options;
using NanoActor.Options;
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

        NanoServiceOptions _serviceOptions;

        
        public RemoteActorMethodInterceptor(
            RemoteStageClient remoteClient,
            ITelemetry telemetry,
            ITransportSerializer serializer,
            IOptions<NanoServiceOptions> serviceOptions,
            TimeSpan? timeout=null,
            Boolean fireAndForget=false            
            )
        {
            _remoteClient = remoteClient;
            _fireAndForget = fireAndForget;
            _serializer = serializer;
            _telemetry = telemetry;
            _serviceOptions = serviceOptions.Value;


#if !RELEASE
            _timeout = timeout??TimeSpan.FromMilliseconds(-1);
#else
            _timeout = timeout ?? TimeSpan.FromSeconds(_serviceOptions.DefaultProxyTimeout);
#endif

        }

        public void InterceptAsynchronous(IInvocation invocation)
        {

            var actor = (IActor)invocation.Proxy;

            
            //List<byte[]> arguments = new List<byte[]>();
            //foreach (var argument in invocation.Arguments)
            //{
            //    arguments.Add(_serializer.Serialize(argument));
            //}
               

            //var message = new ActorRequest()
            //{
            //    ActorId = actor.Id,
            //    ActorInterface = $"{invocation.Method.DeclaringType.Namespace}.{invocation.Method.DeclaringType.Name}",
            //    ActorMethodName = invocation.Method.Name,
            //    Arguments = arguments,
            //    FireAndForget=_fireAndForget
            //};

            var message = new LocalActorRequest()
            {
                ActorId = actor.Id,
                ActorMethod=invocation.Method,
                ArgumentObjects = invocation.Arguments,
                FireAndForget = _fireAndForget
            };


            var task = Task.Run(() =>{


                var tracker = _serviceOptions.TrackProxyDependencyCalls ?
                    _telemetry.Dependency($"proxy.actor:{message.ActorInterface}", message.ActorMethodName) : null;

                return _remoteClient.SendActorRequest(message, _timeout)
                .ContinueWith((t) =>
                {
                    
                    if (t.IsFaulted)
                    {
                        tracker?.End(false);


                        if (t.Exception != null)
                        {
                            throw t.Exception;
                        }
                    }

                    var result = t.Result;
                    if (result!=null && !result.Success)
                    {
                        tracker?.End(false);

                        if (result.Exception != null)
                            throw result.Exception;
                    }

                    tracker?.End(true);


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


            var message = new LocalActorRequest()
            {
                ActorId = actor.Id,
                ActorMethod = invocation.Method,
                ArgumentObjects = invocation.Arguments,
                FireAndForget = _fireAndForget
            };



            var task = Task.Run(async () =>
            {
                var tracker = _serviceOptions.TrackProxyDependencyCalls ?
                    _telemetry.Dependency($"proxy.actor:{message.ActorInterface}", message.ActorMethodName) : null;

                var t = _remoteClient.SendActorRequest(message, _timeout);
                if (_fireAndForget)
                {
                    tracker?.End(true);

                    return default(TResult);
                }
                else
                {
                    try
                    {
                        var result = await t;

                        if (!result.Success)
                        {
                            tracker?.End(false);

                            if (result.Exception != null)
                                throw result.Exception;
                            else
                                throw new Exception("Unexpected Error");

                        }
                        else
                        {
                            tracker?.End(true);

                            var returnType = invocation.Method.ReturnType.GetGenericArguments()[0];

                            var obj = _serializer.Deserialize(returnType, result.Response);

                            return (TResult)(obj);
                        }

                    }
                    catch(Exception ex)
                    {
                        tracker?.End(false);

                        throw ex;
                    }
                    


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
