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
using NanoActor.Redis;

namespace NanoActor.ClusterInstance
{
    public class RedisStage:BaseStage
    {

       

        public RedisStage():base()
        {
          

        }

        

        public override  void ConfigureDefaults()
        {


            base.ConfigureDefaults();

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(IStageDirectory)))
            {
                _serviceCollection.AddSingleton<IStageDirectory, RedisStageDirectory>();
            }
            if (!_serviceCollection.Any(s => s.ServiceType == typeof(IActorDirectory)))
            {
                _serviceCollection.AddSingleton<IActorDirectory, RedisActorDirectory>();
            }



            if (!_serviceCollection.Any(s => s.ServiceType == typeof(ISocketServer)))
            {
                _serviceCollection.AddSingleton<ISocketServer, RedisSocketServer>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(ISocketClient)))
            {
                _serviceCollection.AddSingleton<ISocketClient, RedisSocketClient>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(IPubSub)))
            {
                _serviceCollection.AddSingleton<IPubSub, RedisPubSub>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(RedisConnectionFactory)))
            {
                _serviceCollection.AddSingleton<RedisConnectionFactory>();
            }

            _serviceCollection.Configure<RedisOptions>(_configuration.GetSection("Redis"));
            _serviceCollection.Configure<RedisSocketOptions>(_configuration.GetSection("Redis"));
         

            

        }

        

    }
}
