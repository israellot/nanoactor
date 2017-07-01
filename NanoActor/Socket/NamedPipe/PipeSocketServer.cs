using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NanoActor.Directory;
using System.IO.Pipes;
using Microsoft.Extensions.Options;
using System.IO;
using System.Collections.Concurrent;
using System.Linq;
using NanoActor.Options;
using NanoActor.Util;

namespace NanoActor
{

    


    public class PipeSocketServer : ISocketServer
    {
        

        NanoServiceOptions _serviceOptions;

        public PipeSocketServer(IOptions<NanoServiceOptions> serviceOptions)
        {
            _serviceOptions = serviceOptions.Value;
        }

        protected String DefaultPipeName => $"nano-actor:{_serviceOptions.ServiceName}";

        BufferBlock<SocketData> _inputBuffer = new BufferBlock<SocketData>();

        ConcurrentDictionary<string, NamedPipeServerStream> _pipeClients = new ConcurrentDictionary<string, NamedPipeServerStream>();
        ConcurrentDictionary<string, NamedPipeClientStream> _pipeClientsReturn = new ConcurrentDictionary<string, NamedPipeClientStream>();

        

        protected void StartPipeServer(string pipeName)
        {
            NamedPipeServerStream pipeServer = null;
            string pipeGuid = null;

            pipeServer = new NamedPipeServerStream(pipeName, PipeDirection.In, 254, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

            pipeGuid = Guid.NewGuid().ToString();

            _pipeClients.TryAdd(pipeGuid, pipeServer);

            

            Task.Run(async () => {
                               

                try
                {
                    NamedPipeClientStream returnPipeClient = null;

                    await pipeServer.WaitForConnectionAsync();

                    //start new server
                    StartPipeServer(pipeName);

                    var returnPipeNameBytes = await  NamedPipeUtil.ReadMessageFromPipe(pipeServer);

                    var returnPipeName = Encoding.UTF8.GetString(returnPipeNameBytes);

                    returnPipeClient = new NamedPipeClientStream(".", returnPipeName, PipeDirection.Out, PipeOptions.Asynchronous);

                    await returnPipeClient.ConnectAsync();

                    _pipeClientsReturn.TryAdd(pipeGuid, returnPipeClient);

                    while (true)
                    {

                        var messageData = await NamedPipeUtil.ReadMessageFromPipe(pipeServer);

                        //push message
                        var clientData = new SocketData()
                        {
                            Address = new SocketAddress() { Address = pipeGuid, IsClient = true },
                            Data = messageData
                        };
                        _inputBuffer.Post(clientData);

                    }

                }
                catch (Exception ex)
                {
                    StartPipeServer(pipeName);
                }

                PipeDisconnected(pipeGuid);


            });
        }

        protected void PipeDisconnected(string guid)
        {
            if(_pipeClients.TryGetValue(guid,out var pipe))
            {
                try
                {
                    pipe.Disconnect();
                    pipe.Dispose();
                }
                catch { }
                
            }

            if (_pipeClientsReturn.TryGetValue(guid, out var returnPipe))
            {               
                returnPipe.Dispose();
            }
            
            //remove from client list
            _pipeClients.TryRemove(guid, out _);

            //remove from client list
            _pipeClientsReturn.TryRemove(guid, out _);
        }
                
        public async Task<SocketAddress> Listen()
        {
            var clientPipe = new NamedPipeClientStream(DefaultPipeName);

            bool defaultAlreadyExists = false;
            try
            {
                await clientPipe.ConnectAsync(100);
                defaultAlreadyExists = true;
            }
            catch(Exception ex)
            {

            }

            var pipeName = defaultAlreadyExists ? $"{DefaultPipeName}:{Guid.NewGuid()}" : DefaultPipeName;


            StartPipeServer(pipeName);

            return new SocketAddress()
            {
                Address = pipeName,
                IsStage = true,
                Scheme="named-pipe"
            };
        }

        public Task<SocketData> Receive()
        {
            return _inputBuffer.ReceiveAsync();
        }

        public async Task SendResponse(SocketAddress address, byte[] data)
        {
            
            if(_pipeClientsReturn.TryGetValue(address.Address,out var pipe))
            {
                try
                {
                    if (pipe.IsConnected)
                    {
                        await  NamedPipeUtil.WriteMessageToPipe(pipe, data);
                    }
                    else
                    {
                        PipeDisconnected(address.Address);
                    }                        
                }
                catch(IOException ex)
                {
                    PipeDisconnected(address.Address);
                }

            }

        }


        public int InboundBacklogCount()
        {
            return _inputBuffer.Count;
        }

    }
}
