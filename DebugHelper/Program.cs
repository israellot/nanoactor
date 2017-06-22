using Microsoft.Extensions.DependencyInjection;
using NanoActor;
using NanoActor.ClusterInstance;

using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace DebugHelper
{

    public interface ITestActor : IActor
    {
        Task<string> Hello(string hello);

        Task Throw();
    }

    public class TestActor : Actor, ITestActor
    {
        public async Task<string> Hello(string hello)
        {
            return hello + " World";
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

            var stage = new TcpStage();

            //var stage = new InProcessStage();

            var clientStage = new TcpStageClient();
            clientStage.Configure(c => {
                c.Configure<TcpOptions>(p => { p.Host = "127.0.0.1"; });
                //c.AddSingleton<ITransportSerializer, MsgPackTransportSerializer>();
            });

            stage.Configure(c => {
                c.AddTransient<ITestActor, TestActor>();
                //c.AddSingleton<ITransportSerializer, MsgPackTransportSerializer>();
            });

            stage.Run();

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

            Task.Run(async () => {


                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var proxy = clientStage.ProxyFactory.GetRemoteProxy<ITestActor>("test");

                        var hello = await proxy.Hello("Hello");

                        Interlocked.Increment(ref count);
                    }
                    catch (Exception ex)
                    {

                    }

                }


            }, cts.Token).ConfigureAwait(false);

            Task.Run(async () => {


                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var proxy = clientStage.ProxyFactory.GetRemoteProxy<ITestActor>("test");

                        var hello = await proxy.Hello("Hello");

                        Interlocked.Increment(ref count);
                    }
                    catch (Exception ex)
                    {

                    }

                }



            }, cts.Token).ConfigureAwait(false);

            Task.Run(async () => {

                
                while (!cts.Token.IsCancellationRequested)
                {
                    try
                    {
                        var proxy = clientStage.ProxyFactory.GetRemoteProxy<ITestActor>("test");

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