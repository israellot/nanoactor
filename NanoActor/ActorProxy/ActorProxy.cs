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


        ProxyGenerator _proxyGenerator;

        public ProxyFactory(IServiceProvider services)
        {
            _services = services;
            _proxyGenerator = new ProxyGenerator();
        }



        

        public T GetProxy<T>(string id = null,Boolean fireAndForget=false) where T : class
        {
            
            var proxy = _proxyGenerator.CreateInterfaceProxyWithoutTarget<T>(
                new ActorPropertyInterceptor(),
                new RemoteActorMethodInterceptor(_services.GetRequiredService<RemoteStageClient>(), fireAndForget).ToInterceptor()
                );


            id = id ?? string.Empty;

            var p = proxy as IActor;
            if (p != null)
                p.Id = id;


            return proxy;
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
