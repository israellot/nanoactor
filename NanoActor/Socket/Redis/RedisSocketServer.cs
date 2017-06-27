using Microsoft.Extensions.Options;
using NanoActor.Options;
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

        ISubscriber _subscriber => _multiplexer.Value.GetSubscriber();

        String _inputChannel => $"nano:{_serviceOptions.ServiceName}:{_guid}:*";

        String _outputChannel(string guid) => $"nano:{_serviceOptions.ServiceName}:{guid}:{_guid}";

        String _guid;

        BufferBlock<SocketData> _inputBuffer = new BufferBlock<SocketData>();

        public RedisSocketServer(IServiceProvider services,IOptions<NanoServiceOptions> serviceOptions, IOptions<RedisOptions> redisOptions)
        {
            this._services = services;
            this._serviceOptions = serviceOptions.Value;
            this._redisOptions = redisOptions.Value;

            _guid = _redisOptions.InstanceGuid??Guid.NewGuid().ToString();

            _multiplexer = new Lazy<IConnectionMultiplexer>(() => {

                var m = ConnectionMultiplexer.Connect(_redisOptions.ConnectionString);
                m.PreserveAsyncOrder = false;
                

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
                    Scheme = "redis"
                },
                Data = message
            };

            _inputBuffer.Post(socketData);

        }

        

        public async Task<SocketAddress> Listen()
        {
            var multiplexer = _multiplexer.Value;

            await _subscriber.SubscribeAsync(_inputChannel, (c, v) => {

                MessageReceived(c, v);

            });

            return new SocketAddress()
            {
                Address = _guid,
                IsStage = true,
                Scheme = "redis"
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
    }
}
