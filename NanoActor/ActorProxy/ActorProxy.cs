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
using NanoActor.Telemetry;
using Microsoft.Extensions.Options;
using NanoActor.Options;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace NanoActor.ActorProxy
{
           
    public class ProxyFactory
    {
        IServiceProvider _services;
        
        ProxyGenerator _proxyGenerator;

        PubSubManager _pubsub;

        ITelemetry _telemetry;

        ITransportSerializer _serializer;

        IOptions<NanoServiceOptions> _serviceOptions;

        IMemoryCache _cache;

        public ProxyFactory(
            IServiceProvider services,
            ITelemetry<ProxyFactory> telemetry,
            PubSubManager pubsub,
            ITransportSerializer serializer,
            IOptions<NanoServiceOptions> options
            )
        {
            _pubsub = pubsub;
            _services = services;
            _serializer = serializer;
            _telemetry = telemetry;
            _proxyGenerator = new ProxyGenerator();
            _serviceOptions = options;
            _cache = new MemoryCache(Microsoft.Extensions.Options.Options.Create(new MemoryCacheOptions() { }));
        }

        private object _getProxySyncRoot = new object();
        public T GetProxy<T>(string id = null,TimeSpan? timeout=null,Boolean fireAndForget=false) where T : class
        {
            //var telemetry = _services.GetRequiredService<ITelemetry<T>>();
            //var serviceOptions = _services.GetRequiredService<IOptions<NanoServiceOptions>>();

            var key = new Tuple<Type, string, TimeSpan?, Boolean>(typeof(T), id, timeout, fireAndForget);

            if (!_cache.TryGetValue<T>(key, out var proxy))
            {

                lock (_getProxySyncRoot)
                {
                    if (!_cache.TryGetValue<T>(key, out proxy))
                    {
                        proxy = CreateProxy<T>(id, timeout, fireAndForget);
                        _cache.Set(key, proxy, new MemoryCacheEntryOptions()
                        {
                            SlidingExpiration = TimeSpan.FromMinutes(30)
                        });
                    }
                }
            }

            //var proxy = CreateProxy<T>(id, timeout, fireAndForget);

            return proxy;
        }
                
        protected T CreateProxy<T>(string id = null, TimeSpan? timeout = null, Boolean fireAndForget = false) where T : class
        {
            //var telemetry = _services.GetRequiredService<ITelemetry<T>>();
            //var serviceOptions = _services.GetRequiredService<IOptions<NanoServiceOptions>>();

            var _remoteInterceptor = new RemoteActorMethodInterceptor(
                   _services.GetRequiredService<RemoteStageClient>(),
                   _telemetry,
                   _serializer,
                   _serviceOptions,
                   timeout,
                   fireAndForget).ToInterceptor();

            var proxy = _proxyGenerator.CreateInterfaceProxyWithoutTarget<T>(
                 new ActorPropertyInterceptor(),
               _remoteInterceptor
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


        

    }
}
