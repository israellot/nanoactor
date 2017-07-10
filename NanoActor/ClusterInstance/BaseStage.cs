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
using NanoActor.PubSub;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using NanoActor.Telemetry;

namespace NanoActor.ClusterInstance
{
    public abstract class BaseStage
    {

        protected IServiceCollection _serviceCollection;

        protected IServiceProvider _serviceProvider;

        protected IConfiguration _configuration;

        protected ConfigurationBuilder _configurationBuilder;

        private static ILoggerFactory _loggerFactory = null;

        protected Boolean _configured;

        RemoteStageClient _remoteClient;

        RemoteStageServer _remoteServer;

        IStageDirectory _stageDirectory;

        ITransportSerializer _serializer;

        ITelemetry _telemetry;

        public BaseStage()
        {
            _serviceCollection = new ServiceCollection();

            _configurationBuilder = new ConfigurationBuilder();

            _loggerFactory = new LoggerFactory();

            _serviceCollection.AddSingleton<ILoggerFactory>(_loggerFactory);

            _serviceCollection.AddLogging();

            //_loggerFactory = _serviceCollection.BuildServiceProvider().GetRequiredService<ILoggerFactory>();

            
        }

       

        public void Configure(Action<IServiceCollection,ILoggerFactory, IConfigurationBuilder> configureAction)
        {
            configureAction.Invoke(_serviceCollection, _loggerFactory,_configurationBuilder);

            var service = _serviceCollection.FirstOrDefault(s => s.ImplementationType == typeof(ILoggerFactory));

            Configure();
        }

        public virtual void ConfigureDefaults()
        {
            _serviceCollection.AddOptions();
            

            //if (!_serviceCollection.Any(s => s.ServiceType == typeof(ILoggerFactory)))
            //{
            //    _serviceCollection.AddSingleton<ILoggerFactory>(_loggerFactory);
            //}   

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(ITransportSerializer)))
            {
                _serviceCollection.AddSingleton<ITransportSerializer, MsgPackTransportSerializer>();
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

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(PubSubManager)))
            {
                _serviceCollection.AddSingleton<PubSubManager>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(ProxyFactory)))
            {
                _serviceCollection.AddSingleton<ProxyFactory>();
            }


            _serviceCollection.AddTransient<ITelemetrySink, NullTelemetrySink>();
            _serviceCollection.AddTransient<ITelemetrySink, LoggerTelemetrySink>(s=> {
                var logger = _loggerFactory.CreateLogger("global");
                return new LoggerTelemetrySink(logger);
            });

            _serviceCollection.AddTransient<ITelemetrySink[]>(s => {
                var sinks = s.GetServices<ITelemetrySink>();

                return sinks.ToArray();
            });
            _serviceCollection.AddTransient(typeof(LoggerTelemetrySink<>));
                        
            if (!_serviceCollection.Any(s => s.ServiceType == typeof(ITelemetry)))
            {
                _serviceCollection.AddSingleton<ITelemetry,Telemetry.Telemetry>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(ITelemetry<>)))
            {
                _serviceCollection.AddSingleton(typeof(ITelemetry<>), typeof(Telemetry<>));
            }

            _configuration = _configurationBuilder.Build();

            _serviceCollection.Configure<NanoServiceOptions>(_configuration.GetSection("ServiceOptions"));
            _serviceCollection.Configure<TcpOptions>(_configuration.GetSection("TcpOptions"));
           

        }

        public void Configure()
        {
            ConfigureDefaults();

            _serviceProvider = _serviceCollection.BuildServiceProvider();

            _remoteClient  = _serviceProvider.GetRequiredService<RemoteStageClient>();

            _stageDirectory = _serviceProvider.GetRequiredService<IStageDirectory>();

            _serializer = _serviceProvider.GetRequiredService<ITransportSerializer>();

            _telemetry = _serviceProvider.GetRequiredService<ITelemetry>();

            _configured = true;
        }

        public void RunServer()
        {
            _remoteServer = _serviceProvider.GetRequiredService<RemoteStageServer>();

            Task.Factory.StartNew(_remoteServer.Run);
            

        }

        public async Task<Boolean> Connected()
        {
            var stages = await _stageDirectory.GetAllStages();

           

            foreach(var stage in stages)
            {
                var pingResult = await _remoteClient.PingStage(stage);

                if (pingResult.HasValue)
                    return true;
            }
            return false;

        }

        public ProxyFactory ProxyFactory
        {

            get
            {

                if (!_configured) Configure();

                var factory = _serviceProvider.GetRequiredService<ProxyFactory>();

                return factory;

            }
        }

        public int InboundBacklogCount()
        {
            return _remoteServer.MessageBacklog();
        }

    }
}
