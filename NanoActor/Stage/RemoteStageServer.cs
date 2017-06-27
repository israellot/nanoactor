using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using NanoActor.Directory;

namespace NanoActor
{
    public class RemoteStageServer
    {
        IServiceProvider _services;

        IActorDirectory _actorDirectory;
        LocalStage _localStage;
        ISocketServer _socketServer;       
        ITransportSerializer _serializer;
        IStageDirectory _stageDirectory;

        StageAddress ownAddress;

        Lazy<RemoteStageClient> _remoteClient;

        ConcurrentDictionary<Guid, BufferBlock<ActorResponse>> responseQueues = new ConcurrentDictionary<Guid, BufferBlock<ActorResponse>>();

        public RemoteStageServer(
            IServiceProvider services, 
            IActorDirectory actorDirectory,
            IStageDirectory stageDirectory,
            LocalStage localStage,
            ISocketServer socketServer,            
            ITransportSerializer serializer           

            )
        {
            _services = services;

            _actorDirectory = actorDirectory;

            _stageDirectory = stageDirectory;

            _localStage = localStage;

            _socketServer = socketServer;

            _serializer = serializer;

            _remoteClient = new Lazy<RemoteStageClient>(() => {
                return _services.GetRequiredService<RemoteStageClient>();
            });
                        
            ProcessServerInput();
        }

        public async Task Run()
        {
            
            var address = await _socketServer.Listen();

            ownAddress = await _stageDirectory.RegisterStage(_localStage.StageGuid.ToString(), address);

            _localStage.Enabled = true;
        }

        public void ProcessServerInput()
        {

            Task.Run(async () =>
            {

                while (true)
                {
                    try
                    {
                        var received = await _socketServer.Receive();

                        if (received.Data != null)
                        {
                            var remoteMessage = _serializer.Deserialize<RemoteStageMessage>(received.Data);

                            if (remoteMessage.IsActorResponse)
                            {
                                ReceivedActorResponse(remoteMessage.ActorResponse);
                            }
                            if (remoteMessage.IsActorRequest)
                            {
                                ReceivedActorRequest(remoteMessage.ActorRequest, received.Address).ConfigureAwait(false);
                            }

                        }
                    }
                    catch (Exception ex)
                    {

                    }

                }

            }).ConfigureAwait(false);


        }

        public async Task ReceivedActorRequest(ActorRequest message, SocketAddress sourceAddress)
        {

            if (_localStage == null || !_localStage.Enabled)
            {
                //we are inside a client, no point on getting actor requests
                return;
            }


            if (_localStage.CanProcessMessage(message))
            {
                await ProcessActorRequestLocally(message, sourceAddress);
                return;
            }
            else
            {
                var queryResult = await _actorDirectory.GetAddress(message.ActorInterface, message.ActorId);

                if (queryResult.Found)
                {
                    //TODO forward

                }
                else
                {
                    //no remote actor found, go back and create it locally

                    await ProcessActorRequestLocally(message, sourceAddress);
                    return;

                }
            }



        }       


        public void ReceivedActorResponse(ActorResponse response)
        {
            if (responseQueues.TryGetValue(response.Id, out var buffer))
            {
                buffer.Post(response);
            }

          
        }

        protected async Task ProcessActorRequestLocally(ActorRequest message, SocketAddress sourceAddress)
        {
            var response = await _localStage.Execute(message);

            await this.SendActorResponse(sourceAddress, response);
        }


        public async Task SendActorResponse(SocketAddress address, ActorResponse response)
        {
            if (address == null)
            {
                //message for local client
                _remoteClient.Value.ServerResponse(response);
            }
            else
            {
                var message = new RemoteStageMessage() { IsActorResponse = true, ActorResponse = response };


                await this.SendMessage(address, message);
            }

          
        }

        
        public async Task SendMessage(SocketAddress address,RemoteStageMessage message)
        {           
            message.Destination = address; 

            var transportMessage = _serializer.Serialize(message);

            await _socketServer.SendResponse(address, transportMessage);
        }



    }
}
