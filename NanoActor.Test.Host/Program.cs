using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NanoActor.ClusterInstance;
using NanoActor.Test.Actors;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NanoActor.Test.Host
{
    class Program
    {
        static void Main(string[] args)
        {
            var stage = new RedisStage();



            stage.Configure( (c,log,options) => {

                c.AddTransient<ITestActor, TestActor>();
                //c.AddSingleton<ITransportSerializer, MsgPackTransportSerializer>();

#if DEBUG
                log.AddConsole(LogLevel.Debug).AddDebug();

#else
                log.AddConsole(LogLevel.Error);
#endif

                options.AddJsonFile("appsettings.json");
            });


            Task.Run(() => {
                while (true)
                {
                    Thread.Sleep(30000);

                    stage.StopServer();

                    Console.WriteLine($"Server Backlog: {stage.InboundBacklogCount()}");
                    Console.WriteLine();
                }

            });

            stage.RunServer();


            Console.ReadKey();
        }
    }
}