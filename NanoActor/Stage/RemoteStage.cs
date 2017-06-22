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
    public class RemoteStage
    {
        IServiceProvider _services;

        IActorDirectory _actorDirectory;
        LocalStage _localStage;
        IRemoteSocketManager _socket;
        ITransportSerializer _serializer;

        ConcurrentDictionary<Guid, BufferBlock<ActorResponse>> responseQueues = new ConcurrentDictionary<Guid, BufferBlock<ActorResponse>>();

        public RemoteStage(
            IServiceProvider services, 
            IActorDirectory actorDirectory,
            LocalStage localStage,
            IRemoteSocketManager remoteSocket,
            ITransportSerializer serializer

            )
        {
            _services = services;

            _actorDirectory = actorDirectory;

            _localStage = localStage;

            _socket = remoteSocket;

            _serializer = serializer;

            ProcessRemoteInput();
        }

        public void Run()
        {
            _socket.Listen();

            
        }

        public void ProcessRemoteInput()
        {

            Task.Run(async () => {

                while (true)
                {
                    try
                    {
                        var received = await _socket.Receive();

                        if (received.Data != null)
                        {
                            var remoteMessage = _serializer.Deserialize<RemoteStageMessage>(received.Data);

                            if (remoteMessage.IsActorResponse)
                            {
                                await ReceivedActorResponse(remoteMessage.ActorResponse);
                            }
                            if (remoteMessage.IsActorRequest)
                            {
                                ReceivedActorRequest(remoteMessage.ActorRequest, received.Address).ConfigureAwait(false);
                            }

                        }                        
                    }
                    catch(Exception ex)
                    {

                    }
                    
                }

            });


        }

        public async Task ReceivedActorResponse(ActorResponse response)
        {
            if (responseQueues.TryGetValue(response.Id, out var buffer))
            {
                buffer.Post(response);
            }
        }

        public async Task ReceivedActorRequest(ActorRequest message, StageAddress sourceAddress)
        {

            if (_localStage!=null || !_localStage.Enabled)
            {
                var response = await _localStage.Execute(message);

                if(response is Exception)
                {
                    var actorResponse = new ActorResponse()
                    {
                        Exception = (Exception)response,
                        Id = message.Id
                    };

                    await this.SendActorResponse(sourceAddress, actorResponse);

                }
                else
                {
                    var actorResponse = new ActorResponse()
                    {
                        Response=response,
                        Success=true,
                        Id=message.Id
                    };

                    await this.SendActorResponse(sourceAddress, actorResponse);

                }
            }

        }       
        
        public  Task<StageAddress> LocateActorInstance<ActorType>(string actorId)
        {
            return _actorDirectory.GetAddress<ActorType>(actorId);
        }

        public async Task SendMessage(StageAddress address,RemoteStageMessage message)
        {
            message.Destination = address;
            message.Source = await _socket.LocalAddress();

            var transportMessage = _serializer.Serialize(message);

            await _socket.Send(address, transportMessage);
        }

        public async Task SendActorResponse(StageAddress address,ActorResponse response)
        {
            var message = new RemoteStageMessage() { IsActorResponse = true, ActorResponse = response };

            await this.SendMessage(address, message);
        }

        public async Task<ActorResponse> SendActorRequest(ActorRequest message)
        {
            var address = await _actorDirectory.GetAddress(message.ActorInterface, message.ActorId);

            
            //if no instance was found and we are on a cluster instance
            if (address.NotFound && _localStage !=null && _localStage.Enabled)
            {
                //run locally
                var result = await _localStage.Execute(message);

                if (result is Exception)
                {
                    var response = new ActorResponse()
                    {
                        Success = false,
                        Exception = (Exception)result,
                        Id = message.Id
                    };

                    return response;
                }
                else
                {
                    var response = new ActorResponse()
                    {
                        Success = true,
                        Response = result,
                        Id = message.Id
                    };

                    return response;
                }

            }
            else
            {
                //run remotely
                return await SendRemoteActorRequest(address, message);
            }

        }

        public async Task<ActorResponse> SendRemoteActorRequest(ActorRequest message)
        {
            var address = await _actorDirectory.GetAddress(message.ActorInterface, message.ActorId);

            return await SendRemoteActorRequest(address, message);
        }

        public async Task<ActorResponse> SendRemoteActorRequest(StageAddress address,ActorRequest message)
        {
            
            var waitBuffer = responseQueues.GetOrAdd(message.Id, new BufferBlock<ActorResponse>());

            try
            {
                await this.SendMessage(address, new RemoteStageMessage() { IsActorRequest = true, ActorRequest = message });

                var response = await waitBuffer.ReceiveAsync(TimeSpan.FromSeconds(5));

                return response;
            }
            catch(Exception ex)
            {
                return new ActorResponse() { Exception = ex, Success = false };
            }
            finally{

                waitBuffer.Complete();
                responseQueues.TryRemove(message.Id, out _);
            }

            

        }

    }
}
