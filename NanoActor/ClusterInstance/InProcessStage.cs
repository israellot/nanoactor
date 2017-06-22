using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using NanoActor.Directory;
using NanoActor.ActorProxy;

namespace NanoActor.ClusterInstance
{
    public class InProcessStage
    {

        IServiceCollection _serviceCollection;

        IServiceProvider _serviceProvider;

      

        public InProcessStage()
        {
            _serviceCollection = new ServiceCollection();

        }

        public void Configure(Action<IServiceCollection> configureAction)
        {
            configureAction.Invoke(_serviceCollection);

            Configure();
        }

        public void ConfigureDefaults()
        {
            if(!_serviceCollection.Any(s=>s.ServiceType == typeof(ITransportSerializer)))
            {
                _serviceCollection.AddSingleton<ITransportSerializer, JsonTransportSerializer>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(IActorDirectory)))
            {
                _serviceCollection.AddSingleton<IActorDirectory, MemoryActorDirectory>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(IRemoteSocketManager)))
            {
                _serviceCollection.AddSingleton<IRemoteSocketManager, MemoryRemoteSocket>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(LocalStage)))
            {
                _serviceCollection.AddSingleton<LocalStage>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(RemoteStage)))
            {
                _serviceCollection.AddSingleton<RemoteStage>();
            }

        }

        public void Configure()
        {
            ConfigureDefaults();

            _serviceProvider = _serviceCollection.BuildServiceProvider();

            var remoteStage = _serviceProvider.GetRequiredService<RemoteStage>();

            
        }

        public void Run()
        {
            
            var remoteStage = _serviceProvider.GetRequiredService<RemoteStage>();

            remoteStage.Run();
        }


        public ProxyFactory ProxyFactory
        {
            get { return new ProxyFactory(_serviceProvider); }
        }
        

    }
}
