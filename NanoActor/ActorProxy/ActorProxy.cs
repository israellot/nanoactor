using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using System.Reflection;
using System.Reflection.Emit;
using System.Linq;
using NanoActor.PubSub;

namespace NanoActor.ActorProxy
{
           
    public class ProxyFactory
    {
        IServiceProvider _services;


        ProxyGenerator _proxyGenerator;

        PubSubManager _pubsub;

        public ProxyFactory(IServiceProvider services,PubSubManager pubsub)
        {
            _pubsub = pubsub;
            _services = services;
            _proxyGenerator = new ProxyGenerator();
        }
                                

        public T GetProxy<T>(string id = null,TimeSpan? timeout=null,Boolean fireAndForget=false) where T : class
        {
            
            var proxy = _proxyGenerator.CreateInterfaceProxyWithoutTarget<T>(
                new ActorPropertyInterceptor(),
                new RemoteActorMethodInterceptor(_services.GetRequiredService<RemoteStageClient>(),timeout, fireAndForget).ToInterceptor()
                );


            id = id ?? string.Empty;

            var p = proxy as IActor;
            if (p != null)
                p.Id = id;


            return proxy;
        }

        public ActorEventProxy GetEventProxy<T>()
        {
            return new ActorEventProxy(_pubsub, typeof(T).Name, null);
        }

        public ActorEventProxy GetEventProxy<T>(string actorId)
        {
            return new ActorEventProxy(_pubsub, $"{typeof(T).Namespace}.{typeof(T).Name}", actorId);
        }


        static class RemoteProxyCache<T> where T:class
        {
            private static T _instance;

            public static T GetOrAdd(Func<T> createFunction)
            {
                if (_instance == null)
                    _instance = createFunction();                

                return _instance;
            }
            
        }

    }
}
