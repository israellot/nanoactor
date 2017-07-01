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

namespace NanoActor.ClusterInstance
{
    public class ZeroMQStage:BaseStage
    {

       

        public ZeroMQStage():base()
        {
           

        }


        public void ConfigureDefaults()
        {
           

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(ISocketServer)))
            {
                _serviceCollection.AddSingleton<ISocketServer, ZMQSocketServer>();
            }

            if (!_serviceCollection.Any(s => s.ServiceType == typeof(ISocketClient)))
            {
                _serviceCollection.AddSingleton<ISocketClient, ZMQSocketClient>();
            }

            base.ConfigureDefaults();

        }

       
        

    }
}
