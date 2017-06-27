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

namespace DebugHelper
{

    public interface ITestActor : IActor
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

        

        public static void Main(string[] args)
        {

            var stage = new ZeroMQStage();

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

            var clientStage = new ZeroMQStage();

            clientStage.ConfigureOptions(options => {
                options.AddJsonFile("appsettings.json");
            });

            clientStage.Configure();

            var testProxy = clientStage.ProxyFactory.GetProxy<ITestActor>("test");
            testProxy.NoReturn("Hello").Wait();

            Console.ReadKey();

            testProxy = clientStage.ProxyFactory.GetProxy<ITestActor>("test1");
            testProxy.NoReturn("Hello").Wait();

            testProxy = clientStage.ProxyFactory.GetProxy<ITestActor>("test2");
            testProxy.NoReturn("Hello").Wait();

            

            CancellationTokenSource cts = new CancellationTokenSource();

            int count = 0;
            Stopwatch sw = Stopwatch.StartNew();

            Task.Run(() => {
                while (true)
                {
                    Thread.Sleep(3000);

                    Console.WriteLine($"{ (double)count / ((double)sw.ElapsedMilliseconds / 1000.0):f2} req/s");
                    Console.WriteLine($"{ ((double)sw.ElapsedMilliseconds)/(double)count:f2} ms/rq");
                    Console.WriteLine();
                }
                
            });

           

            

            Task.Run(async () =>
            {
                var proxy = clientStage.ProxyFactory.GetProxy<ITestActor>("test1");

                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        //await Task.Delay(5000);

                        var hello = await proxy.Hello("Hello");

                        if (hello != "Hello World test1")
                        {
                            var a = "";
                        }

                        Interlocked.Increment(ref count);
                    }
                    catch (Exception ex)
                    {

                    }

                }


            }, cts.Token).ConfigureAwait(false);

            Task.Run(async () =>
            {

                var proxy = clientStage.ProxyFactory.GetProxy<ITestActor>("test2");
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        //await Task.Delay(5000);

                        var hello = await proxy.Hello("Hello");

                        if (hello != "Hello World test2")
                        {
                            var a = "";
                        }

                        Interlocked.Increment(ref count);
                    }
                    catch (Exception ex)
                    {

                    }

                }


            }, cts.Token).ConfigureAwait(false);


            Task.Run(async () =>
            {

                var proxy = clientStage.ProxyFactory.GetProxy<ITestActor>("test3");
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {

                        //await Task.Delay(5000);
                        var hello = await proxy.Hello("Hello");

                        Interlocked.Increment(ref count);
                    }
                    catch (Exception ex)
                    {

                    }

                }


            }, cts.Token).ConfigureAwait(false);



            Console.ReadKey();

            cts.Cancel();

            

        }
    }
}