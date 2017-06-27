using Microsoft.Extensions.Options;
using NanoActor.Options;
using NanoActor.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Polly;
using System.Threading;

namespace NanoActor
{
    public class PipeSocketClient : ISocketClient
    {
        NanoServiceOptions _serviceOptions;

        protected String DefaultPipeName => $"nano-actor:{_serviceOptions.ServiceName}";

        ConcurrentDictionary<string, NamedPipeClientStream> _pipes = new ConcurrentDictionary<string, NamedPipeClientStream>();

        BufferBlock<SocketData> _inputBuffer = new BufferBlock<SocketData>();

        Policy _retryPolicy = Policy.Handle<TimeoutException>().RetryAsync(3);

        public PipeSocketClient(IOptions<NanoServiceOptions> serviceOptions)
        {
            _serviceOptions = serviceOptions.Value;
        }

        protected void Listen(string address,string pipeName)
        {
                        
            var pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                                    
            Task.Run(async () => {
                try
                {
                    await pipeServer.WaitForConnectionAsync();

                    while (true)
                    {
                        var message = await  NamedPipeUtil.ReadMessageFromPipe(pipeServer);

                        //push message
                        var clientData = new SocketData()
                        {
                            Address = new SocketAddress() { Address = address, IsClient = true },
                            Data = message
                        };
                        _inputBuffer.Post(clientData);

                    }
                }catch(Exception ex)
                {

                }
                
            });

           
        }


        static SemaphoreSlim _pipeConnectSemaphore = new SemaphoreSlim(1, 1);
        public async Task SendRequest(SocketAddress address, byte[] data)
        {
            if (address == null || address.Address == null)
                address = new SocketAddress() { Address = DefaultPipeName };
            try
            {
             

                if(!_pipes.TryGetValue(address.Address,out var pipe)){

                    await _pipeConnectSemaphore.WaitAsync();

                    if (!_pipes.TryGetValue(address.Address, out pipe))
                    {
                        pipe = new NamedPipeClientStream(".", address.Address, PipeDirection.Out, PipeOptions.Asynchronous);

                        try
                        {
                            var pipeGuid = Guid.NewGuid();
                            var listenPipeName = $"{DefaultPipeName}:client:{pipeGuid.ToString()}";
                            this.Listen(address.Address, listenPipeName);

                            await pipe.ConnectAsync(5000);

                            await  NamedPipeUtil.WriteMessageToPipe(pipe, listenPipeName);

                            _pipes.TryAdd(address.Address, pipe);
                        }
                        finally
                        {
                            _pipeConnectSemaphore.Release();
                        }

                    }

                }
                               
                              
                await  NamedPipeUtil.WriteMessageToPipe(pipe,data);
             
            }
            catch(Exception ex)
            {
                
            }
            
        }


        public async Task<SocketData> Receive()
        {
            return await _inputBuffer.ReceiveAsync();
        }
    }
}
