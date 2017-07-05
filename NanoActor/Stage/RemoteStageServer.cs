using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using NanoActor.Directory;
using System.Threading;
using System.Linq;
using Microsoft.Extensions.Logging;

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

        StageAddress _ownAddress;
        Lazy<RemoteStageClient> _remoteClient;
        ConcurrentDictionary<Guid, BufferBlock<ActorResponse>> responseQueues = new ConcurrentDictionary<Guid, BufferBlock<ActorResponse>>();

        ConcurrentDictionary<string, ConcurrentQueue<Tuple<ActorRequest, SocketAddress>>> _pausedActorMessageQueues = new ConcurrentDictionary<string, ConcurrentQueue<Tuple<ActorRequest, SocketAddress>>>();

        volatile Int32 _inputProccessBacklog = 0;

        ILogger _logger;

        public RemoteStageServer(
            IServiceProvider services, 
            IActorDirectory actorDirectory,
            IStageDirectory stageDirectory,
            LocalStage localStage,
            ILogger<RemoteStageServer> logger,
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

            _logger = logger;

            _remoteClient = new Lazy<RemoteStageClient>(() => {
                return _services.GetRequiredService<RemoteStageClient>();
            });
                        
            ProcessServerInput();

            
        }

        public async Task Run()
        {
            _localStage.Run();

            var address = await _socketServer.Listen();

            _ownAddress = await _stageDirectory.RegisterStage(_localStage.StageGuid, address);

            MonitorOtherStages();

            SelfMonitor();

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

                        Interlocked.Increment(ref _inputProccessBacklog);

                        Task.Run(async () =>
                        {
                            

                            if (received.Data != null)
                            {
                                var remoteMessage = _serializer.Deserialize<RemoteStageMessage>(received.Data);

                                if (remoteMessage.IsActorResponse)
                                {
                                    ReceivedActorResponse(remoteMessage.ActorResponse);
                                }
                                if (remoteMessage.IsActorRequest)
                                {
                                   
                                    await ReceivedActorRequest(remoteMessage.ActorRequest, received.Address);

                                }
                                if (remoteMessage.IsPingRequest)
                                {
                                    await ProcessPingRequest(received.Address, remoteMessage.Ping);
                                }

                                Interlocked.Decrement(ref _inputProccessBacklog);
                            }
                        }).ConfigureAwait(false);

                        
                    }
                    catch (Exception ex)
                    {

                    }

                }

            }).ConfigureAwait(false);


        }

        public void SelfMonitor()
        {
            Task.Run(async () => {

                Dictionary<string, int> _missedPings = new Dictionary<string, int>();

                while (true)
                {
                    await Task.Delay(5000);
                    try
                    {
                        var stages = await _stageDirectory.GetAllStages();

                        if (!stages.Contains(_ownAddress.StageId))
                        {
                            //others have signaled this stage as dead

                            await _stageDirectory.RegisterStage(_localStage.StageGuid, _ownAddress.SocketAddress);
                        }

                    }
                    catch
                    {

                    }
                }


            }).ConfigureAwait(false);
        }

        public void MonitorOtherStages()
        {
            Task.Run(async () => {

                ConcurrentDictionary<string, int> _missedPings = new ConcurrentDictionary<string, int>();

                while (true)
                {
                    await Task.Delay(1000);

                    try
                    {
                        var stages = await _stageDirectory.GetAllStages();

                        stages = stages.Where(s => s != _ownAddress.StageId).ToList();

                        foreach (var stage in stages)
                        {
                            var localStage = stage;
                            var task = Task.Run(async () =>
                            {

                                var pingResponse = await _remoteClient.Value.PingStage(localStage);

                                if (pingResponse == null)
                                {
                                    
                                    var count = _missedPings.AddOrUpdate(localStage, 1, (s, c) => {
                                         return c+1;
                                        });

                                    _logger.LogDebug("Stage {0} missed ping, count: {1}", localStage, count);

                                    if (count > 5)
                                    {
                                        await _stageDirectory.UnregisterStage(localStage);
                                        _logger.LogDebug("Stage {0} removed", localStage, count);
                                        _missedPings.TryRemove(localStage, out _);
                                    }
                                }
                                else
                                {
                                    _missedPings.TryRemove(localStage, out _);
                                }

                            }).ConfigureAwait(false);

                            
                        }

                    }
                    catch(Exception ex)
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


            
            if (_pausedActorMessageQueues.TryGetValue(string.Join(":", message.ActorInterface, message.ActorId), out var queue))
            {
                //actor is being delayed, queue
                queue.Enqueue(new Tuple<ActorRequest, SocketAddress>(message, sourceAddress));
                return;
            }
            
            //if (_localStage.CanProcessMessage(message))
            //{
            //    await ProcessActorRequestLocally(message, sourceAddress);
            //    return;
            //}
            //else
            //{
            var queryResult = await _actorDirectory.GetAddress(message.ActorInterface, message.ActorId);

                if (queryResult.Found)
                {
                    if (queryResult.StageId == _localStage.StageGuid)
                    {
                        await ProcessActorRequestLocally(message, sourceAddress);
                        return;
                    }
                    else
                    {
                        if (await _stageDirectory.GetStageAddress(queryResult.StageId)!=null)
                        {
                            var remoteResponse = await _remoteClient.Value.SendRemoteActorRequest(queryResult.StageId, message);
                            if (!message.FireAndForget)
                            {
                                await this.SendActorResponse(sourceAddress, remoteResponse);
                            }
                        }
                        else
                        {
                            //the stage is no longer active

                            //reallocate the actor
                            var newStageId = await _actorDirectory.Reallocate(message.ActorInterface, message.ActorId, queryResult.StageId);

                            //try again
                            await ReceivedActorRequest(message, sourceAddress);
                        }
                        
                    }                    
                }
                
            //}



        }       

        public async Task RelocateActor(string actorTypeName, String actorId,String newStageId=null)
        {
            var key = string.Join(":", actorTypeName, actorId);
            var queue = new ConcurrentQueue<Tuple<ActorRequest,SocketAddress>>();
            if (_pausedActorMessageQueues.TryAdd(key, queue)){

                try
                {
                    //wait idle
                    await _localStage.WaitInstanceIdle(actorTypeName, actorId);

                    if (newStageId == null)
                    {
                        newStageId = (await _stageDirectory.GetAllStages()).Where(s => s != _ownAddress.StageId).FirstOrDefault();

                        if (newStageId == null)
                            throw new Exception("No other stage running");

                        //switch address
                        await _actorDirectory.RegisterActor(actorTypeName, actorId, newStageId);
                        await _actorDirectory.Refresh(actorTypeName, actorId);

                        
                    }
                }
                finally
                {
                    _pausedActorMessageQueues.TryRemove(key, out _);
                    while (queue.TryDequeue(out var request))
                    {
                        this.ReceivedActorRequest(request.Item1, request.Item2);
                    }
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

            if (!message.FireAndForget)
            {
                await this.SendActorResponse(sourceAddress, response);
            }

           
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

        public Task ProcessPingRequest(SocketAddress address,Ping ping)
        {
            var message = new RemoteStageMessage()
            {
                Ping = ping,
                IsPingReponse = true
            };

            return SendMessage(address, message);

        }
        
        public async Task SendMessage(SocketAddress address,RemoteStageMessage message)
        {           
            message.Destination = address;
            message.Source = _ownAddress.SocketAddress;

            var transportMessage = _serializer.Serialize(message);

            await _socketServer.SendResponse(address, transportMessage);
        }

        public Int32 MessageBacklog()
        {
            return (Int32)_inputProccessBacklog;
        }

        public Int32 SocketMessageBacklog()
        {
            return _socketServer.InboundBacklogCount();
        }

    }
}
