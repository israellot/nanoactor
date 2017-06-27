using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NanoActor.Directory;
using NetMQ.Sockets;
using Microsoft.Extensions.Options;
using System.Threading.Tasks.Dataflow;
using NetMQ;

namespace NanoActor
{
    public class ZMQRemoteSocket : IRemoteSocketManager
    {
        RouterSocket _routerSocket;
        NetMQ.NetMQPoller _pooler;

        TcpOptions _options;

        BufferBlock<IRemoteSocketData> _inputBuffer;

        public ZMQRemoteSocket(IServiceProvider services, IOptions<TcpOptions> options)
        {
            _options = options.Value;

            _inputBuffer = new BufferBlock<IRemoteSocketData>();
        }

        public Task Listen()
        {
            _routerSocket = new RouterSocket();
            _routerSocket.Bind($"tcp://{_options.Host}:{_options.Port}");

            _routerSocket.ReceiveReady += _routerSocket_ReceiveReady;

            _pooler = new NetMQ.NetMQPoller();
            _pooler.Add(_routerSocket);

            _pooler.RunAsync();

            return Task.FromResult(0);

        }

        protected StageAddress GetAddress(NetMQSocket client)
        {            
            return new TcpAddress()
            {
                IsLocal = false,
                Address = $"tcp://{ client.Options.LastEndpoint.ToString() }"
            };
        }

        private void _routerSocket_ReceiveReady(object sender, NetMQ.NetMQSocketEventArgs e)
        {
            NetMQ.NetMQMessage msg = new NetMQ.NetMQMessage();
            if(e.Socket.TryReceiveMultipartMessage(ref msg,3))
            {
                var address = msg.Pop();
                var empty = msg.Pop();
                var data = msg.Pop();


                var messageData = new MemoryRemoteSocketData()
                {
                    Address = new StageAddress() { Address = address.ConvertToString() },
                    Data = data.Buffer
                };

                _inputBuffer.Post(messageData);
            }
        }

        public async Task<StageAddress> LocalAddress()
        {
            return new TcpAddress()
            {
                Address = $"tcp://{_options.Host}:{_options.Port}"
            };
        }

        public Task<IRemoteSocketData> Receive(TimeSpan? timeout = default(TimeSpan?), CancellationToken? ct = default(CancellationToken?))
        {
            return _inputBuffer.ReceiveAsync(timeout ?? TimeSpan.FromMilliseconds(-1), ct ?? CancellationToken.None);
        }

        public async Task<bool> Send(StageAddress address, byte[] data)
        {
            if (address.Address == null)
                address = await LocalAddress();

            //try send on known peers
            

            var reqSocket = new NetMQ.Sockets.DealerSocket();

            reqSocket.Options.Identity = Guid.NewGuid().ToByteArray();

            reqSocket.Connect(address.Address);

            var messageToServer = new NetMQMessage();
            messageToServer.AppendEmptyFrame();
            messageToServer.Append(data);

            reqSocket.SendMultipartMessage(messageToServer);

            return true;
        }
    }
}
