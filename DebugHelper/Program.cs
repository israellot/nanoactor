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
using NanoActor.Test.Actors;
using Microsoft.Extensions.Logging.Console;
using Microsoft.Extensions.Logging;
using MessagePack;

namespace DebugHelper
{

   

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

            

            //client

            var clientStage = new RedisStage();

           

            clientStage.Configure((c, log,options) => {
                options.AddJsonFile("appsettings.json");
                //log.AddConsole();
                //log.AddDebug(LogLevel.Debug);
            });

            

         

            while (true)
            {
                try
                {
                    if (clientStage.Connected().Result)
                        break;
                    else
                        Console.WriteLine("failed to connect");

                }
                catch(Exception ex)
                {
                    Console.WriteLine("failed to connect");
                }
                Thread.Sleep(3000);
            }

            

            Console.WriteLine("Press Key");
            Console.ReadKey();

            var lockObject = new object();


            CancellationTokenSource cts = new CancellationTokenSource();

            long req = 0;
            long resp = 0;
            Stopwatch sw = Stopwatch.StartNew();

            long completeRequests = 0;
            double totalTime = 0;

            Task.Run(() => {
                while (true)
                {
                    Interlocked.Exchange(ref resp,0);
                    Interlocked.Exchange(ref req, 0);
                    Interlocked.Exchange(ref completeRequests, 0);
                    Interlocked.Exchange(ref totalTime, 0);

                    sw.Restart();
                    Thread.Sleep(5000);

                    var totalElapsedMs = sw.Elapsed.TotalMilliseconds;

                    double totalMs;
                    long totalComplete;
                    lock (lockObject)
                    {
                        totalMs = totalTime;
                        totalComplete = Interlocked.Read(ref completeRequests);
                    }

                    Console.WriteLine($"{ totalComplete / 1000.0:f2} K requests , {totalElapsedMs:f2} ms");
                    Console.WriteLine($"{ (double)totalComplete / 1000.0 / totalElapsedMs*1000:f2} K req/s");
                    Console.WriteLine($"{ totalMs / (double)totalComplete:f2} ms/rq");
                    Console.WriteLine($"Local Backlog: {req - resp}");                    
                    Console.WriteLine();
                }
                
            });

            var stop = false;


            for(int i = 0; i < 500; i++)
            {

                var iCopy = i;

                Task.Run(async () =>
                {
                    await Task.Delay(50 * iCopy);
                    Console.WriteLine($"Starting worker {iCopy}");

                    var proxy = clientStage.ProxyFactory.GetProxy<ITestActor>("test"+ iCopy.ToString());

                    var localSw = Stopwatch.StartNew();

                    while (!cts.Token.IsCancellationRequested)
                    {
                        try
                        {
                            if (stop)
                                break;

                            Interlocked.Increment(ref req);

                            localSw.Restart();                                                        
                            var helloResponse = await proxy.Hello("Hello");
                            localSw.Stop();

                            Interlocked.Increment(ref resp);
                            Interlocked.Increment(ref completeRequests);

                            lock (lockObject)
                            {
                                totalTime = totalTime + localSw.Elapsed.TotalMilliseconds;
                            }

                            
                            
                            if (helloResponse != $"Hello World test" + iCopy)
                            {
                                var a = "";
                            }


                            //SpinWait.SpinUntil(() => { return req - resp < 100* iCopy; });


                        }
                        catch(TimeoutException timeout)
                        {
                            Console.WriteLine("Timeout");
                        }
                        catch (Exception ex)
                        {

                        }

                    }


                }, cts.Token).ConfigureAwait(false);

            }


            Console.WriteLine("Press key to stop");
            Console.ReadKey();

            stop = true;

            Console.ReadKey();


            cts.Cancel();

            

        }
    }
}