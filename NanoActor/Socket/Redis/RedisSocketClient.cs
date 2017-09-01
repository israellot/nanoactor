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

        BufferBlock<SocketData> _inputBuffer = new BufferBlock<SocketData>();

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

            _guid = Guid.NewGuid().ToString().Substring(0, 8);

            _multiplexer = ConnectionMultiplexer.Connect(_redisOptions.ConnectionString);
            _multiplexer.PreserveAsyncOrder = false;
            

            _logger.LogInformation($"Client connected to Redis instance: {_multiplexer.IsConnected}");

            

            Listen();
        }

        protected void MessageReceived(string channel,byte[] message)
        {

            

            var remoteGuid = channel.Split(':').Last();

            var socketData = new SocketData()
            {
                Address = new SocketAddress()
                {
                    Address = remoteGuid,
                    Scheme = "redis",
                    StageId=remoteGuid
                },
                Data = message
            };

            _inputBuffer.Post(socketData);

        }
                

        protected void Listen()
        {
                                   
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

        public async Task<SocketData> Receive()
        {
            return await _inputBuffer.ReceiveAsync();
        }

        

        public async Task SendRequest(SocketAddress address, byte[] data)
        {
                        
            if (address==null || address.Address == null)
            {
                var stages = await _stageDirectory.GetAllStages();

                //shuffle
                var stageId = stages.OrderBy(s => Guid.NewGuid()).FirstOrDefault();

                address = (await _stageDirectory.GetStageAddress(stageId))?.SocketAddress;


            }

            if (address == null)
            {
                _logger.LogError("No live stage to connect to");

                throw new Exception("No live stage to connect to");
            }

            try
            {
                _redisPolicy.Execute(() => {
                    var result = _subscriber.Publish(_outputChannel(address.Address), data, CommandFlags.FireAndForget | CommandFlags.HighPriority);
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
