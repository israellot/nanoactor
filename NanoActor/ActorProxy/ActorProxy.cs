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


namespace NanoActor.ActorProxy
{
           
    public class ProxyFactory
    {
        IServiceProvider _services;

        public ProxyFactory(IServiceProvider services)
        {
            _services = services;
        }

        public T GetLocalProxy<T>(string id=null) where T:class
        {
            id = id ?? string.Empty;
            
            var generator = new Castle.DynamicProxy.ProxyGenerator();

            var proxy = generator.CreateInterfaceProxyWithoutTarget<T>(
                new ActorPropertyInterceptor(),
                new LocalActorMethodInterceptor(_services.GetRequiredService<LocalStage>()).ToInterceptor()
                );

            var p = proxy as IActor;
            if (p != null)            
                p.Id = id;
                                    
                        
            return proxy;
        }

        

        public T GetRemoteProxy<T>(string id = null) where T : class
        {

            var proxy = RemoteProxyCache<T>.GetOrAdd(() => {
                var generator = new Castle.DynamicProxy.ProxyGenerator();

                return generator.CreateInterfaceProxyWithoutTarget<T>(
                    new ActorPropertyInterceptor(),
                    new RemoteActorMethodInterceptor(_services.GetRequiredService<RemoteStage>()).ToInterceptor()
                    );
            });
            

            id = id ?? string.Empty;

            var p = proxy as IActor;
            if (p != null)
                p.Id = id;


            return proxy;
        }

        static class LocalProxyCache<T> where T : class
        {
            private static T _instance;

            public static T GetOrAdd(Func<T> createFunction)
            {
                if (_instance == null)
                    _instance = createFunction();

                return _instance;
            }


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
