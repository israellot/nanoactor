using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using NanoActor.Directory;
using System.Threading;
using NanoActor.Telemetry;
using System.Linq;
using System.Diagnostics;
using NanoActor.Util;

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
        ITelemetry _telemetry;


        ConcurrentDictionary<Guid, Tuple<SemaphoreSlim, ActorResponse>> _localResponseBuffer = new ConcurrentDictionary<Guid, Tuple<SemaphoreSlim, ActorResponse>>();
        ConcurrentDictionary<Guid, Tuple<SemaphoreSlim, ActorResponse>> _serverResponseBuffer = new ConcurrentDictionary<Guid, Tuple<SemaphoreSlim, ActorResponse>>();
        ConcurrentDictionary<Guid, SemaphoreSlim> _pingResponseBuffer = new ConcurrentDictionary<Guid, SemaphoreSlim>();

        ObjectPool<SemaphoreSlim> _semaphorePool = new ObjectPool<SemaphoreSlim>(()=>new SemaphoreSlim(0,1));

        public RemoteStageClient(
            IServiceProvider services,
            IActorDirectory actorDirectory,
            IStageDirectory stageDirectory,
            LocalStage localStage,
            ISocketClient socketClient,
            ITransportSerializer serializer,
            RemoteStageServer stageServer,
            ITelemetry telemetry
            )
        {
            _services = services;

            _actorDirectory = actorDirectory;

            _stageDirectory = stageDirectory;

            _localStage = localStage;

            _socketClient = socketClient;

            _serializer = serializer;

            _stageServer = stageServer;

            _telemetry = telemetry;

            ProcessServerInput();
        }

        public void ProcessServerInput()
        {

            _socketClient.DataReceived += (s, e) =>
            {

                Task.Run(() =>
                {
                    try
                    {
                        var received = e.SocketData;

                        if (received.Data != null)
                        {
                            var remoteMessage = _serializer.Deserialize<RemoteStageMessage>(received.Data);

                            if (remoteMessage.MessageType == RemoteMessageType.ActorResponse)
                            {
                                var messageId = remoteMessage.ActorResponse.Id;
                                if (_serverResponseBuffer.TryGetValue(messageId, out var queueItem))
                                {
                                    var semaphore = queueItem.Item1;
                                    _serverResponseBuffer[messageId] = new Tuple<SemaphoreSlim, ActorResponse>(queueItem.Item1, remoteMessage.ActorResponse);
                                    semaphore.Release();
                                }


                            }
                            if (remoteMessage.MessageType == RemoteMessageType.PingResponse)
                            {
                                if (_pingResponseBuffer.TryGetValue(remoteMessage.Ping.Id, out var semaphore))
                                {
                                    semaphore.Release();
                                }
                            }

                        }


                    }
                    catch (Exception ex)
                    {
                        _telemetry.Exception(ex);
                    }
                }).ConfigureAwait(false);

            };

        }

        public void ServerResponse(ActorResponse response)
        {
            if (_localResponseBuffer.TryGetValue(response.Id, out var queueItem))
            {
                var semaphore = queueItem.Item1;
                _localResponseBuffer[response.Id] = new Tuple<SemaphoreSlim, ActorResponse>(queueItem.Item1, response);
                semaphore.Release();
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
                    var semaphore = _semaphorePool.GetObject();
                    while (semaphore.CurrentCount > 0) semaphore.Wait();

                    var queueItem = _localResponseBuffer[request.Id] = new Tuple<SemaphoreSlim, ActorResponse>(semaphore, null);

                    try
                    {
                        await _stageServer.ReceivedActorRequest(request, null);

                        var responseArrived = await semaphore.WaitAsync(timeout.Value);

                        if (responseArrived)
                        {
                            if (_localResponseBuffer.TryRemove(request.Id, out queueItem))
                            {
                                if (queueItem.Item2 != null)
                                {
                                    return queueItem.Item2;
                                }
                            }
                        }

                        _localResponseBuffer.TryRemove(request.Id, out _);
                        throw new TimeoutException();
                    }
                    finally
                    {
                        _semaphorePool.PutObject(semaphore);

                    }


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


            if (request.WorkerActor)
            {
                var allStages = await _stageDirectory.GetAllStages();
                var stageAddress = allStages.OrderBy(a => Guid.NewGuid()).FirstOrDefault();
                return await SendRemoteActorRequest(stageAddress, request, timeout);
            }
            else
            {
                var stageResponse = await _actorDirectory.GetAddress(request.ActorInterface, request.ActorId);
                if (await _stageDirectory.IsLive(stageResponse.StageId))
                {
                    return await SendRemoteActorRequest(stageResponse.StageId, request, timeout);
                }
                else
                {
                    return await SendRemoteActorRequest(null, request, timeout);
                }

            }


        }

        public async Task<ActorResponse> SendRemoteActorRequest(String stageId, ActorRequest request, TimeSpan? timeout = null)
        {

            if (request is LocalActorRequest)
                request = ((LocalActorRequest)request).ToRemote(_serializer);

            var message = new RemoteStageMessage()
            {
                ActorRequest = request,
                MessageType = RemoteMessageType.ActorRequest
            };

            if (request.FireAndForget)
            {
                await _socketClient.SendRequest(stageId, _serializer.Serialize(message));

                return new ActorResponse() { Success = true };
            }
            else
            {
                var semaphore = _semaphorePool.GetObject();
                while (semaphore.CurrentCount > 0) semaphore.Wait();

                var queueItem = _serverResponseBuffer[request.Id] = new Tuple<SemaphoreSlim, ActorResponse>(semaphore, null);

                try
                {
                    await _socketClient.SendRequest(stageId, _serializer.Serialize(message));

                    var responseArrived = await semaphore.WaitAsync(timeout ?? TimeSpan.FromMilliseconds(-1));
                   
                    if (responseArrived)
                    {
                        if (_serverResponseBuffer.TryRemove(request.Id, out queueItem))
                        {
                            return queueItem.Item2;
                        }
                    }

                    _serverResponseBuffer.TryRemove(request.Id, out _);
                    throw new TimeoutException();
                }
                finally
                {
                    _semaphorePool.PutObject(semaphore);
                }

               

            }

        }



        public async Task<TimeSpan?> PingStage(String stageId)
        {

            var message = new RemoteStageMessage()
            {
                MessageType = RemoteMessageType.PingRequest,
                Ping = new Ping()
            };


            var semaphore = _pingResponseBuffer[message.Ping.Id] = _semaphorePool.GetObject();

            var sw = Stopwatch.StartNew();

            try
            {
                await _socketClient.SendRequest(stageId, _serializer.Serialize(message));

                if (await semaphore.WaitAsync(1000))
                {
                    sw.Stop();
                    return sw.Elapsed;
                    //return TimeSpan.FromTicks(DateTimeOffset.UtcNow.Ticks - message.Ping.Timestamp);
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
                _semaphorePool.PutObject(semaphore);
                _pingResponseBuffer.TryRemove(message.Ping.Id, out _);
                sw.Stop();
            }

        }

    }
}
