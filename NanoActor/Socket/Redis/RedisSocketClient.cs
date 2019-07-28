using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NanoActor.Options;
using NanoActor.Redis;
using NanoActor.Telemetry;
using Polly;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace NanoActor.Socket.Redis
{
    public class RedisSocketClient : ISocketClient
    {
        IServiceProvider _services;

        IConnectionMultiplexer _multiplexer;

        NanoServiceOptions _serviceOptions;

        RedisOptions _redisOptions;

        ISubscriber _subscriber => _multiplexer.GetSubscriber();

        Policy _redisPolicy = RedisRetry.RetryPolicy;

        IStageDirectory _stageDirectory;

        ILogger _logger;

        String _inputChannel => $"nano:{_serviceOptions.ServiceName}:{_guid}:*";
        String _outputChannel(string guid) => $"nano:{_serviceOptions.ServiceName}:{guid}:{_guid}";


        String _guid;

        ITelemetry _telemetry;

        public event EventHandler<DataReceivedEventArgs> DataReceived;


        public RedisSocketClient(
            IServiceProvider services,
            ITelemetry telemetry,
            ILogger<RedisSocketClient> logger,
            IStageDirectory stageDirectory,
            IOptions<NanoServiceOptions> serviceOptions,
            IOptions<RedisOptions> redisOptions)
        {
            this._services = services;
            this._serviceOptions = serviceOptions.Value;
            this._redisOptions = redisOptions.Value;
            this._stageDirectory = stageDirectory;
            this._telemetry = telemetry;

            _logger = logger;

            _guid = Guid.NewGuid().ToString();

            

            Listen();
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

        

        }
                

        protected void Listen()
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

                    _multiplexer.InternalError += (sender, e) => {
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
                catch (Exception ex)
                {
                    _logger.LogCritical("Failed to connect to Redis");
                }
                
            }
            
            _multiplexer.PreserveAsyncOrder = false;
            _logger.LogInformation($"Client connected to Redis instance: {_multiplexer.IsConnected}");


            try
            {
                _subscriber.Subscribe(new RedisChannel(_inputChannel,RedisChannel.PatternMode.Pattern), (c, v) => {

                    MessageReceived(c, v);

                });
                _logger.LogInformation($"Client listening on channel {_inputChannel}");

            }
            catch(Exception ex)
            {
                _logger.LogCritical("Failed to start listener");                              
            }
            
            
        }

       

        public async Task SendRequest(string stageId, byte[] data)
        {
            if (!_multiplexer.IsConnected)
                throw new Exception("Redis not connected");

            if (String.IsNullOrEmpty(stageId))
            {
                var stages = await _stageDirectory.GetAllStages();

                //shuffle
                stageId = stages.OrderBy(s => Guid.NewGuid()).FirstOrDefault();
            }

            if (String.IsNullOrEmpty(stageId))
            {
                _logger.LogError("No live stage to connect to");

                throw new Exception("No live stage to connect to");
            }

            try
            {
                _redisPolicy.Execute(() => {
                    var result = _subscriber.Publish(_outputChannel(stageId), data, CommandFlags.FireAndForget | CommandFlags.HighPriority);
                });                
            }
            catch (Exception ex)
            {
                _telemetry.Exception(ex);
                _logger.LogError("Client Failed to send request");              
            }





        }
    }
}
