﻿using Microsoft.Extensions.Configuration;
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

                log.AddConsole(LogLevel.Debug).AddDebug();

                options.AddJsonFile("appsettings.json");
            });

            stage.Run();


            Task.Run(() => {
                while (true)
                {
                    Thread.Sleep(3000);

               
                    Console.WriteLine($"Server Backlog: {stage.InboundBacklogCount()}");
                    Console.WriteLine();
                }

            });


            Console.ReadKey();
        }
    }
}