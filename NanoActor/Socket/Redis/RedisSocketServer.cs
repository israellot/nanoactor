using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NanoActor.Options;
using NanoActor.Redis;
using NanoActor.Telemetry;
using Polly;
using StackExchange.Redis;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace NanoActor.Socket.Redis
{

   

    public class RedisSocketServer : ISocketServer
    {
        IServiceProvider _services;

        IConnectionMultiplexer _multiplexer;

        NanoServiceOptions _serviceOptions;

        RedisOptions _redisOptions;

        ILogger _logger;

        ISubscriber _subscriber => _multiplexer.GetSubscriber();

        ITelemetry _telemetry;

        Policy _redisPolicy = RedisRetry.RetryPolicy;

        String _inputChannel => $"nano:{_serviceOptions.ServiceName}:{_guid}:*";

        String _outputChannel(string guid) => $"nano:{_serviceOptions.ServiceName}:{guid}:{_guid}";

        String _guid;

        public event EventHandler<DataReceivedEventArgs> DataReceived;

        LocalStage _localStage;

        MetricTracker _redisPingTracker;

        public RedisSocketServer(
            IServiceProvider services,
            LocalStage localStage,
            ITelemetry telemetry,
            ILogger<RedisSocketServer> logger,
            IOptions<NanoServiceOptions> serviceOptions,
            IOptions<RedisOptions> redisOptions)
        {
            this._services = services;
            this._serviceOptions = serviceOptions.Value;
            this._redisOptions = redisOptions.Value;
            this._localStage = localStage;
            this._telemetry = telemetry;

            _logger = logger;

        }

        protected void MessageReceived(string channel,byte[] message)
        {
           

            var remoteGuid = channel.Split(':').Last();

            var socketData = new SocketData()
            {
               StageId= remoteGuid,
                Data = message
            };

            DataReceived?.Invoke(this, new DataReceivedEventArgs() { SocketData = socketData });

            //_inputBuffer.Post(socketData);

        }

       

        public async Task<Boolean> Listen()
        {


            while (true)
            {
                try
                {
                    ConfigurationOptions config = ConfigurationOptions.Parse(_redisOptions.ConnectionString);
                    config.ConnectTimeout = 10000;
                    config.ConnectRetry = Int32.MaxValue;
                    config.SyncTimeout = 2500;
                    config.ResolveDns = true;

                    _multiplexer = ConnectionMultiplexer.Connect(config);

                    _multiplexer.InternalError+= (sender,e)=> {
                        if (e.Exception != null)
                            _telemetry.Exception(e.Exception);                                                
                    };

                    _multiplexer.ConnectionFailed += (sender, e) => {
                        if (e.Exception != null)
                            _telemetry.Exception(e.Exception);                                                
                    };

                    _multiplexer.ConnectionRestored += (sender, e) =>
                    {
                        if (e.Exception != null)
                            _telemetry.Exception(e.Exception);
                    };

                    break;
                }
                catch(Exception ex)
                {
                    _logger.LogCritical("Failed to connect to Redis");
                }
                await Task.Delay(1000);

            }

            _multiplexer.PreserveAsyncOrder = false;

            _guid = _localStage.StageGuid;

          
            try
            {
                await _subscriber.SubscribeAsync(new RedisChannel(_inputChannel, RedisChannel.PatternMode.Pattern), (c, v) => {

                    MessageReceived(c, v);

                });
                _logger.LogInformation($"Listening on channel {_inputChannel}");
            }
            catch (Exception ex)
            {
                _telemetry.Exception(ex);
                _logger.LogCritical("Failed to start listener");
            }

            RedisPing();

            return true;
        }

        public async Task RedisPing()
        {
            _redisPingTracker = _telemetry.Metric("Stage.Redis.Ping", new Dictionary<string, string>() { ["FromStage"] = _guid });

            while (true)
            {
                try
                {
                    await Task.Delay(5000);
                    var ping = await _multiplexer.GetDatabase().PingAsync();
                    _redisPingTracker.Track(ping.TotalMilliseconds);                    
                }
                catch (Exception ex) { }
            }
        }

       

        public Task SendResponse(string stageId, byte[] data)
        {
            _redisPolicy.Execute(() => {
                _subscriber.Publish(_outputChannel(stageId), data, CommandFlags.FireAndForget | CommandFlags.HighPriority);
            });

            return Task.CompletedTask;
        }
      
    }
}
