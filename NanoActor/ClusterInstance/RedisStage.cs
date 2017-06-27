using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using NanoActor.Directory;
using NanoActor.ActorProxy;
using Microsoft.Extensions.Options;
using NanoActor.Options;
using Microsoft.Extensions.Configuration;
using NanoActor.Socket.Redis;

namespace NanoActor.ClusterInstance
{
    public class RedisStage
    {

        IServiceCollection _serviceCollection;

        IServiceProvider _serviceProvider;

        IConfiguration _configuration;

        ConfigurationBuilder _configurationBuilder;

        Boolean _configured;

        public RedisStage()
        {
            _serviceCollection = new ServiceCollection();

            _configurationBuilder = new ConfigurationBuilder();

        }

        public void ConfigureOptions(Action<ConfigurationBuilder> configuration)
        {

            configuration.Invoke(_configurationBuilder);
                       
        }

        public void Configure(Action<IServiceCollection> configureAction)
        {
            configureAction.Invoke(_serviceCollection);

            Configure();
        }

        public void ConfigureDefaults()
        {
            _serviceCollection.AddOptions();

            if (!_serviceCollection.Any(s=>s.ServiceType == typeof(ITransportSerializer)))
            {
                _serviceCollection.AddSingleton<ITransportSerializer, MsgPackTransportSerializer>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(IStageDirectory)))
            {
                _serviceCollection.AddSingleton<IStageDirectory, MemoryStageDirectory>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(IActorDirectory)))
            {
                _serviceCollection.AddSingleton<IActorDirectory, MemoryActorDirectory>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(ISocketServer)))
            {
                _serviceCollection.AddSingleton<ISocketServer, RedisSocketServer>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(ISocketClient)))
            {
                _serviceCollection.AddSingleton<ISocketClient, RedisSocketClient>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(LocalStage)))
            {
                _serviceCollection.AddSingleton<LocalStage>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(RemoteStageServer)))
            {
                _serviceCollection.AddSingleton<RemoteStageServer>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(RemoteStageClient)))
            {
                _serviceCollection.AddSingleton<RemoteStageClient>();
            }

            _configuration = _configurationBuilder.Build();

            _serviceCollection.Configure<NanoServiceOptions>(_configuration.GetSection("ServiceOptions"));
            _serviceCollection.Configure<TcpOptions>(_configuration.GetSection("TcpOptions"));
            _serviceCollection.Configure<RedisOptions>(_configuration.GetSection("Redis"));

        }

        public void Configure()
        {
            ConfigureDefaults();

            _serviceProvider = _serviceCollection.BuildServiceProvider();

            var remoteClient = _serviceProvider.GetRequiredService<RemoteStageClient>();

            _configured = true;
        }

        public void Run()
        {
            
            var remoteServer = _serviceProvider.GetRequiredService<RemoteStageServer>();

            remoteServer.Run().Wait();

            
            
        }


        public ProxyFactory ProxyFactory
        {
            
            get {

                if (!_configured) Configure();

                return new ProxyFactory(_serviceProvider);

            }
        }
        

    }
}
