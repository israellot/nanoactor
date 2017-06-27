using Microsoft.Extensions.DependencyInjection;
using NanoActor;
using NanoActor.ClusterInstance;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using NanoActor.Options;
using StackExchange.Redis;

namespace DebugHelper
{

    public interface ITestActor :IActor
    {
        Task<string> Hello(string hello);

        Task NoReturn(string hello);

        Task Throw();
    }

    public class TestActor : Actor, ITestActor
    {
        public Task<string> Hello(string hello)
        {
            return Task.FromResult(hello + " World " + this.Id);
        }

        public async Task NoReturn(string hello)
        {
            await Task.Delay(50);
        }

        public async Task Throw()
        {
            throw new Exception("");
        }
    }

    class Program
    {

        public static void Main2(string[] args)
        {
            var multiplexer = ConnectionMultiplexer.Connect("localhost");

            var multiplexer2 = ConnectionMultiplexer.Connect("localhost");

            var req = 0;
            var resp = 0;

            var sub1 = multiplexer.GetSubscriber();
            var sub2 = multiplexer2.GetSubscriber();

           

            sub1.Subscribe("in1", (c, v) => {

                Interlocked.Increment(ref req);

                sub1.PublishAsync("in2", "return", CommandFlags.FireAndForget);

            });

            multiplexer2.GetSubscriber().Subscribe("in2", (c, v) => {

                Interlocked.Increment(ref resp);
                //sub2.Publish("in1", "message", CommandFlags.FireAndForget);

            });

            //sub2.Publish("in1", "message", CommandFlags.FireAndForget);

            Task.Run(() =>
            {

                while (true)
                {
                    sub2.Publish("in1", "message", CommandFlags.FireAndForget);
                    SpinWait.SpinUntil(() => { return req - resp == 0; });
                }

            });

            Stopwatch sw = Stopwatch.StartNew();
            Task.Run(async () => {
                while (true)
                {
                    await Task.Delay(3000);

                    Console.WriteLine($"{ (double)req / ((double)sw.ElapsedMilliseconds / 1000.0):f2} req/s");

                    Console.WriteLine($"Difference: {req-resp}");
                }
                

            });

            Console.ReadKey();

        }

        public static void Main(string[] args)
        {

            var stage = new RedisStage();

            var dict = new Dictionary<string, string>
            {
                {"ServiceOptions:ServiceName", "TestService"}
            };

            stage.ConfigureOptions(options => {
                options.AddJsonFile("appsettings.json");
            });
            
            

            stage.Configure(c => {
                                
                c.AddTransient<ITestActor, TestActor>();
                //c.AddSingleton<ITransportSerializer, MsgPackTransportSerializer>();
            });



            stage.Run();



            //client

            var clientStage = new RedisStage();

            clientStage.ConfigureOptions(options => {
                options.AddJsonFile("appsettings.json");
            });

            clientStage.Configure();

            var testProxy = clientStage.ProxyFactory.GetProxy<ITestActor>("test",true);
            testProxy.NoReturn("Hello").Wait();

            Console.ReadKey();

            testProxy = clientStage.ProxyFactory.GetProxy<ITestActor>("test1",true);
            testProxy.NoReturn("Hello").Wait();

            testProxy = clientStage.ProxyFactory.GetProxy<ITestActor>("test2",true);
            testProxy.NoReturn("Hello").Wait();

            

            CancellationTokenSource cts = new CancellationTokenSource();

            int req = 0;
            int resp = 0;
            Stopwatch sw = Stopwatch.StartNew();

            Task.Run(() => {
                while (true)
                {
                    Thread.Sleep(3000);

                    Console.WriteLine($"{ (double)req / ((double)sw.ElapsedMilliseconds / 1000.0):f2} req/s");
                    Console.WriteLine($"{ ((double)sw.ElapsedMilliseconds)/(double)req:f2} ms/rq");
                    Console.WriteLine($"Backlog: {req - resp}");
                    Console.WriteLine();
                }
                
            });

           

            for(int i = 0; i < 10; i++)
            {

                Task.Run(() =>
                {
                    var proxy = clientStage.ProxyFactory.GetProxy<ITestActor>("test"+i.ToString(),true);

                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            Interlocked.Increment(ref req);

                            proxy.Hello("Hello").ContinueWith((s) => {
                                Interlocked.Increment(ref resp);

                                if(s.Result!=$"Hello World test" + i)
                                {
                                    var a = "";
                                }

                            });


                            SpinWait.SpinUntil(() => { return req - resp < 100*i; });


                        }
                        catch (Exception ex)
                        {

                        }

                    }


                }, cts.Token).ConfigureAwait(false);

            }

           

     



            Console.ReadKey();

            cts.Cancel();

            

        }
    }
}