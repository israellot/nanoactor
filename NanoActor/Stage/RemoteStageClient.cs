using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using NanoActor.Directory;
using System.Threading;

namespace NanoActor
{

    

    public class RemoteStageClient
    {
        IServiceProvider _services;

        IActorDirectory _actorDirectory;
        LocalStage _localStage;
        ISocketClient _socketClient;
        ITransportSerializer _serializer;
        IStageDirectory _stageDirectory;

        RemoteStageServer _stageServer;
               
        
        ConcurrentDictionary<string, BufferBlock<ActorResponse>> _serverResponseBuffer = new ConcurrentDictionary<string, BufferBlock<ActorResponse>>();
        ConcurrentDictionary<string, SemaphoreSlim> _pingResponseBuffer = new ConcurrentDictionary<string, SemaphoreSlim>();



        public RemoteStageClient(
            IServiceProvider services,
            IActorDirectory actorDirectory,
            IStageDirectory stageDirectory,
            LocalStage localStage,
            ISocketClient socketClient,
            ITransportSerializer serializer,
            RemoteStageServer stageServer
            )
        {
            _services = services;

            _actorDirectory = actorDirectory;

            _stageDirectory = stageDirectory;

            _localStage = localStage;

            _socketClient = socketClient;

            _serializer = serializer;

            _stageServer = stageServer;

            ProcessServerInput();
        }

        public void ProcessServerInput()
        {

            Task.Run(async () =>
            {

                while (true)
                {
                    try
                    {
                        var received = await _socketClient.Receive();

                        if (received.Data != null)
                        {
                            var remoteMessage = _serializer.Deserialize<RemoteStageMessage>(received.Data);

                            if (remoteMessage.IsActorResponse)
                            {
                                if (_serverResponseBuffer.TryGetValue(remoteMessage.ActorResponse.Id.ToString(), out var buffer))
                                {
                                    buffer.Post(remoteMessage.ActorResponse);
                                }

                                
                            }
                            if (remoteMessage.IsPingReponse)
                            {
                                if (_pingResponseBuffer.TryGetValue(remoteMessage.Ping.Id.ToString(), out var semaphore))
                                {
                                    semaphore.Release();
                                }
                            }

                        }


                    }
                    catch (Exception ex)
                    {

                    }

                }

            }).ConfigureAwait(false);


        }

        public void ServerResponse(ActorResponse response)
        {

            if(_serverResponseBuffer.TryGetValue(response.Id.ToString(),out var buffer)){
                buffer.Post(response);
            }
            
        }

        public async Task<ActorResponse> SendActorRequest(ActorRequest request, TimeSpan? timeout = null)
        {
            timeout = timeout ?? TimeSpan.FromMilliseconds(-1);

            if (_localStage.Enabled)
            {
                //we are inside a stage, forward to server

                if (request.FireAndForget)
                {
                    await _stageServer.ReceivedActorRequest(request, null);
                    return new ActorResponse() { Success = true };
                }
                else
                {
                    var buffer = _serverResponseBuffer.GetOrAdd(request.Id.ToString(), new BufferBlock<ActorResponse>());

                    await _stageServer.ReceivedActorRequest(request, null);

                    var response = await buffer.ReceiveAsync(timeout.Value);

                    buffer.Complete();
                    _serverResponseBuffer.TryRemove(request.Id.ToString(), out _);

                    return response;
                }
              
            }
            else
            {
                //we are on a pure client

                return await SendRemoteActorRequest(request, timeout);
            }

            
        }

        public async Task<ActorResponse> SendRemoteActorRequest(ActorRequest request, TimeSpan? timeout = null)
        {

            var stageResponse = await _actorDirectory.GetAddress(request.ActorInterface, request.ActorId);
            var stageAddress = await _stageDirectory.GetStageAddress(stageResponse.StageId);
            return await SendRemoteActorRequest(stageAddress, request, timeout);
            
        }

        public async Task<ActorResponse> SendRemoteActorRequest(String stageId, ActorRequest request, TimeSpan? timeout = null)
        {

            var stageSocketAddress = await _stageDirectory.GetStageAddress(stageId);

            return await SendRemoteActorRequest(stageSocketAddress, request, timeout);

        }

        public async Task<ActorResponse> SendRemoteActorRequest(StageAddress stageAddress, ActorRequest request, TimeSpan? timeout = null)
        {

            var message = new RemoteStageMessage()
            {
                ActorRequest = request,
                IsActorRequest = true                
            };

            if (request.FireAndForget)
            {
                await _socketClient.SendRequest(stageAddress == null ? null : stageAddress.SocketAddress, _serializer.Serialize(message));

                return new ActorResponse() { Success = true };
            }
            else
            {
                var buffer = _serverResponseBuffer.GetOrAdd(request.Id.ToString(), new BufferBlock<ActorResponse>());

                await _socketClient.SendRequest(stageAddress == null ? null : stageAddress.SocketAddress, _serializer.Serialize(message));

                var response = await buffer.ReceiveAsync(timeout ?? TimeSpan.FromMilliseconds(-1));

                buffer.Complete();
                _serverResponseBuffer.TryRemove(request.Id.ToString(), out _);

                return response;
            }

            

        }



        public async Task<TimeSpan?> PingStage(String stageId)
        {
            var stageSocketAddress = await _stageDirectory.GetStageAddress(stageId);

            var message = new RemoteStageMessage()
            {               
                IsPingRequest = true,
                Ping = new Ping()
            };

            var semaphore = _pingResponseBuffer.GetOrAdd(message.Ping.Id.ToString(), new SemaphoreSlim(0,1));

            try
            {
                await _socketClient.SendRequest(stageSocketAddress.SocketAddress, _serializer.Serialize(message));

                if (await semaphore.WaitAsync(1000))
                {
                    return TimeSpan.FromMilliseconds(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - message.Ping.Timestamp);
                }
                else
                {
                    return null;
                }
            }
            catch
            {
                return null;
            }
            finally
            {
                _pingResponseBuffer.TryRemove(message.Ping.Id.ToString(), out _);
            }
            
        }

    }
}
