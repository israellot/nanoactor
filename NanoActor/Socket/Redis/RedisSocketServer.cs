using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NanoActor.Options;
using NanoActor.Redis;
using StackExchange.Redis;
using System;
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

        Lazy<IConnectionMultiplexer> _multiplexer;

        NanoServiceOptions _serviceOptions;

        RedisOptions _redisOptions;

        ILogger _logger;

        ISubscriber _subscriber => _multiplexer.Value.GetSubscriber();

        String _inputChannel => $"nano:{_serviceOptions.ServiceName}:{_guid}:*";

        String _outputChannel(string guid) => $"nano:{_serviceOptions.ServiceName}:{guid}:{_guid}";

        String _guid;

        BufferBlock<SocketData> _inputBuffer = new BufferBlock<SocketData>();

        LocalStage _localStage;

        public RedisSocketServer(IServiceProvider services, LocalStage localStage, ILogger<RedisSocketServer> logger,IOptions<NanoServiceOptions> serviceOptions, IOptions<RedisOptions> redisOptions)
        {
            this._services = services;
            this._serviceOptions = serviceOptions.Value;
            this._redisOptions = redisOptions.Value;
            this._localStage = localStage;

            _logger = logger;

            _guid = Guid.NewGuid().ToString().Substring(0, 8);

            _multiplexer = new Lazy<IConnectionMultiplexer>(() => {

                var m = ConnectionMultiplexer.Connect(_redisOptions.ConnectionString);
                m.PreserveAsyncOrder = false;
                _logger.LogInformation($"Client connected to Redis instance: {m.IsConnected}");

                return m;

            });
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

        

        public async Task<SocketAddress> Listen()
        {
            var multiplexer = _multiplexer.Value;

            _guid = _localStage.StageGuid.Substring(0, 8);

          
            try
            {
                await _subscriber.SubscribeAsync(new RedisChannel(_inputChannel, RedisChannel.PatternMode.Pattern), (c, v) => {

                    MessageReceived(c, v);

                });
                _logger.LogInformation($"Listening on channel {_inputChannel}");
            }
            catch (Exception ex)
            {
                _logger.LogCritical("Failed to start listener");
            }

            

            return new SocketAddress()
            {
                Address = _guid,
                IsStage = true,
                Scheme = "redis",
                StageId= _localStage.StageGuid
            };
        }

        public async Task<SocketData> Receive()
        {
            return await _inputBuffer.ReceiveAsync();
        }

        public Task SendResponse(SocketAddress address, byte[] data)
        {
            
            _subscriber.Publish(_outputChannel(address.Address), data, CommandFlags.FireAndForget | CommandFlags.HighPriority );

            return Task.CompletedTask;
        }

        public int InboundBacklogCount()
        {
            return _inputBuffer.Count;
        }
    }
}
