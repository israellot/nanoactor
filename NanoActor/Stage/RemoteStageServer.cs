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
using MoreLinq;
using NanoActor.Telemetry;

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
        ITelemetry _telemetry;

        String _ownStageId;

        Lazy<RemoteStageClient> _remoteClient;
        ConcurrentDictionary<Guid, BufferBlock<ActorResponse>> responseQueues = new ConcurrentDictionary<Guid, BufferBlock<ActorResponse>>();

        ConcurrentDictionary<string, ConcurrentQueue<Tuple<ActorRequest, string>>> _pausedActorMessageQueues = new ConcurrentDictionary<string, ConcurrentQueue<Tuple<ActorRequest, string>>>();

        volatile Int32 _inputProccessBacklog = 0;

        volatile Int32 _paused = 0;

        ILogger _logger;

        public RemoteStageServer(
            IServiceProvider services, 
            IActorDirectory actorDirectory,
            IStageDirectory stageDirectory,
            LocalStage localStage,
            ILogger<RemoteStageServer> logger,
            ISocketServer socketServer,            
            ITransportSerializer serializer,
            ITelemetry telemetry

            )
        {
            _services = services;

            _actorDirectory = actorDirectory;

            _stageDirectory = stageDirectory;

            _localStage = localStage;

            _socketServer = socketServer;

            _serializer = serializer;

            _logger = logger;

            _telemetry = telemetry;

            _remoteClient = new Lazy<RemoteStageClient>(() => {
                return _services.GetRequiredService<RemoteStageClient>();
            });
                        
            ProcessServerInput();

            
        }

        public async Task Run()
        {
            _localStage.Run();

            var address = await _socketServer.Listen();

            _ownStageId = _localStage.StageGuid;

            await _stageDirectory.RegisterStage(_localStage.StageGuid);

            MonitorOtherStages();

            SelfMonitor();

        }

        public void Stop()
        {
            //pause incomming queues
            Interlocked.Exchange(ref _paused, 1);

            //stop local stage
            _localStage.Stop();

            //forward all requests from now on
            _localStage.Enabled = false;
            
            //unregister stage
            _stageDirectory.UnregisterStage(_ownStageId);

            Thread.Sleep(5000);

            foreach (var queue in _pausedActorMessageQueues)
            {
                while (queue.Value.TryDequeue(out var request))
                {
                    this.ReceivedActorRequest(request.Item1, request.Item2);
                }
            }

            Thread.Sleep(5000);

        }


        public void ProcessServerInput()
        {

            foreach(var i in Enumerable.Range(0, 10))
            {

                Task.Factory.StartNew(async () =>
                {

                    while (true)
                    {
                        try
                        {
                            var received = await _socketServer.Receive();

                            

                            Interlocked.Increment(ref _inputProccessBacklog);

                            if (received.Data != null)
                            {
                                var remoteMessage = _serializer.Deserialize<RemoteStageMessage>(received.Data);

                                if (remoteMessage == null)
                                {
                                    Interlocked.Decrement(ref _inputProccessBacklog);
                                    return;
                                }

                                if (remoteMessage.MessageType==RemoteMessageType.ActorResponse)
                                {
                                    ReceivedActorResponse(remoteMessage.ActorResponse);
                                }
                                if (remoteMessage.MessageType==RemoteMessageType.ActorRequest)
                                {
                                    Task.Factory.StartNew(() =>
                                    {
                                        return ReceivedActorRequest(remoteMessage.ActorRequest, received.StageId);
                                    }, TaskCreationOptions.PreferFairness);

                                }
                                if (remoteMessage.MessageType==RemoteMessageType.PingRequest)
                                {
                                    await ProcessPingRequest(received.StageId, remoteMessage.Ping).ConfigureAwait(false);
                                }

                                Interlocked.Decrement(ref _inputProccessBacklog);
                            }



                        }
                        catch (Exception ex)
                        {
                            _telemetry.Exception(ex);
                        }

                    }

                },TaskCreationOptions.LongRunning);


            }

          


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

                        if (!stages.Contains(_ownStageId))
                        {
                            //others have signaled this stage as dead

                            //clear local stage
                            await _localStage.Stop();

                            //register again
                            await _stageDirectory.RegisterStage(_localStage.StageGuid);
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

                List<string> stages = new List<string>();

                ConcurrentDictionary<string, MetricTracker> _metrics = new ConcurrentDictionary<string, MetricTracker>();

                var stageCountMetric = _telemetry.Metric("Stage.Count");

                while (true)
                {
                    await Task.Delay(1000);                    

                    try
                    {
                        var newStages = await _stageDirectory.GetAllStages();

                        if (!newStages.SequenceEqual(stages))
                        {
                            _logger.LogDebug($"Stages: {string.Join(",", newStages)}");
                        }
                        stages = newStages;

                        var otherStages = newStages.Where(s => s != _ownStageId).ToList();

                        stageCountMetric.Track(stages.Count);

                        foreach (var stage in otherStages)
                        {
                            var _stageRef = stage;
                            var task = Task.Run(async () =>
                            {

                                var pingResponse = await _remoteClient.Value.PingStage(_stageRef);

                                if (pingResponse == null)
                                {
                                    
                                    var count = _missedPings.AddOrUpdate(_stageRef, 1, (s, c) => {
                                         return c+1;
                                        });

                                    _logger.LogDebug("Stage {0} missed ping, count: {1}", _stageRef, count);

                                    if (count > 5)
                                    {
                                        await _stageDirectory.UnregisterStage(_stageRef);
                                        await _actorDirectory.RemoveStage(_stageRef);

                                        _logger.LogDebug("Stage {0} removed", _stageRef, count);
                                        _missedPings.TryRemove(_stageRef, out _);

                                        _metrics.TryRemove(_stageRef, out _);
                                    }
                                }
                                else
                                {
                                    var metric = _metrics.GetOrAdd(_stageRef,
                                        _telemetry.Metric($"Stage.Ping", new Dictionary<string, string>() {
                                            {"FromStage",_ownStageId},
                                            {"ToStage",_stageRef }
                                        }));


                                    metric.Track(pingResponse.Value.TotalMilliseconds);

                                    _missedPings.TryRemove(_stageRef, out _);
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

        public async Task ReceivedActorRequest(ActorRequest message, string sourceStageId)
        {
                       
            if (_localStage == null || !_localStage.Enabled)
            {
                //we are inside a client, no point on getting actor requests
                return;
            }

            var pausedQueueKey = string.Join(":", message.ActorInterface, message.ActorId);
            if (!_pausedActorMessageQueues.TryGetValue(pausedQueueKey, out var queue))
            {
                if (_paused == 1 && _localStage.CanProcessMessage(message))
                {
                    var newQueue = new ConcurrentQueue<Tuple<ActorRequest, string>>();
                    if (_pausedActorMessageQueues.TryAdd(pausedQueueKey, newQueue))
                    {
                        queue = newQueue;
                    }
                }
            }
            if (queue != null)
            {
                //actor is being delayed, queue
                queue.Enqueue(new Tuple<ActorRequest, string>(message, sourceStageId));
                return;
            }
         

            if (message.WorkerActor)
            {

                await ProcessActorRequestLocally(message, sourceStageId);
                return;

            }

            if (_localStage.CanProcessMessage(message))
            {
                //we already have a local instance
                await ProcessActorRequestLocally(message, sourceStageId);
                return;
            }

            //query the directory
            var queryResult = await _actorDirectory.GetAddress(message.ActorInterface, message.ActorId);
            if (queryResult.Found)
            {

                if (queryResult.StageId == _localStage.StageGuid)
                {
                    //instance already registered on this stage but not active
                    await ProcessActorRequestLocally(message, sourceStageId);
                    return;
                }
                else
                {
                    if (await _stageDirectory.IsLive(queryResult.StageId))
                    {
                        //proxy request to the right stage
                        var remoteResponse = await _remoteClient.Value.SendRemoteActorRequest(queryResult.StageId, message);
                        if (!message.FireAndForget)
                        {
                            await this.SendActorResponse(sourceStageId, remoteResponse);
                        }
                    }
                    else
                    {
                        //reallocate the actor
                        var newStageId = await _actorDirectory.Reallocate(message.ActorInterface, message.ActorId, queryResult.StageId);

                        //try again
                        await ReceivedActorRequest(message, sourceStageId);
                    }
                    
                }
            }
            else
            {
                //reallocate the actor
                var newStageId = await _actorDirectory.Reallocate(message.ActorInterface, message.ActorId, queryResult.StageId);

                //try again
                await ReceivedActorRequest(message, sourceStageId);

            }



        }       

        public async Task RelocateActor(string actorTypeName, String actorId,String newStageId=null)
        {
            var key = string.Join(":", actorTypeName, actorId);
            var queue = new ConcurrentQueue<Tuple<ActorRequest,string>>();
            if (_pausedActorMessageQueues.TryAdd(key, queue)){

                try
                {
                    //wait idle
                    await _localStage.WaitInstanceIdle(actorTypeName, actorId);

                    if (newStageId == null)
                    {
                        newStageId = (await _stageDirectory.GetAllStages()).Where(s => s != _ownStageId).FirstOrDefault();

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

        protected async Task ProcessActorRequestLocally(ActorRequest message, string sourceStageId)
        {
            ActorResponse response = null;
            try {
                response = await _localStage.Execute(message);
            }
            catch(Exception ex)
            {
                response = new ActorResponse()
                {
                    Id = message.Id,
                    //Exception = ex,
                    Success = false
                };

                _telemetry.Exception(ex, "ProcessActorRequestLocally");
            }

            if (!message.FireAndForget)
            {
                await this.SendActorResponse(sourceStageId, response);
            }


        }


        public async Task SendActorResponse(string stageId, ActorResponse response)
        {
            if (String.IsNullOrEmpty(stageId))
            {
                //message for local client
                _remoteClient.Value.ServerResponse(response);
            }
            else
            {
                var message = new RemoteStageMessage() {MessageType=RemoteMessageType.ActorResponse, ActorResponse = response };


                await this.SendMessage(stageId, message);
            }

          
        }

        public Task ProcessPingRequest(string stageId,Ping ping)
        {
            var message = new RemoteStageMessage()
            {
                Ping = ping,
                MessageType = RemoteMessageType.PingResponse
            };

            return SendMessage(stageId, message);

        }
        
        public async Task SendMessage(String stageId,RemoteStageMessage message)
        {           
            message.Destination = stageId;
            message.Source = _ownStageId;

            var transportMessage = _serializer.Serialize(message);

            await _socketServer.SendResponse(stageId, transportMessage);
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
