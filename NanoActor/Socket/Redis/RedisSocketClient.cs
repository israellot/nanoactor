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
    public class RedisSocketClient : ISocketClient
    {
        IServiceProvider _services;

        IConnectionMultiplexer _multiplexer;

        NanoServiceOptions _serviceOptions;

        RedisOptions _redisOptions;

        ISubscriber _subscriber => _multiplexer.GetSubscriber();

        String _inputChannel => $"nano:{_serviceOptions.ServiceName}:{_guid}:*";

        String _outputChannel(string guid) => $"nano:{_serviceOptions.ServiceName}:{guid}:{_guid}";

        String _guid;

        BufferBlock<SocketData> _inputBuffer = new BufferBlock<SocketData>();

        public RedisSocketClient(IServiceProvider services,IOptions<NanoServiceOptions> serviceOptions, IOptions<RedisOptions> redisOptions)
        {
            this._services = services;
            this._serviceOptions = serviceOptions.Value;
            this._redisOptions = redisOptions.Value;

            _guid = Guid.NewGuid().ToString().Substring(0, 8);

            _multiplexer = ConnectionMultiplexer.Connect(_redisOptions.ConnectionString);
            _multiplexer.PreserveAsyncOrder = false;
            

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
                    Scheme = "redis"
                },
                Data = message
            };

            _inputBuffer.Post(socketData);

        }
                

        protected void Listen()
        {
            

            _subscriber.Subscribe(_inputChannel, (c, v) => {

                MessageReceived(c, v);

            });

            
        }

        public async Task<SocketData> Receive()
        {
            return await _inputBuffer.ReceiveAsync();
        }
                
        public Task SendRequest(SocketAddress address, byte[] data)
        {
            if(address==null || address.Address == null)
            {
                address = new SocketAddress()
                {
                    Scheme = "redis",
                    Address = _redisOptions.InstanceGuid
                };
            }

            _subscriber.Publish(_outputChannel(address.Address), data, CommandFlags.FireAndForget|CommandFlags.HighPriority);


            return Task.CompletedTask;
        }
    }
}
