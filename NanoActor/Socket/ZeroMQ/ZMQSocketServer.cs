using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using Microsoft.Extensions.Options;
using System.Threading.Tasks.Dataflow;
using System.Collections.Concurrent;
using System.Linq;

namespace NanoActor
{

    public class TcpOptions
    {
        public int Port { get; set; } = 34567;

        public string Host { get; set; } = "0.0.0.0";

        public int MaxClientSockets { get; set; } = 3;
    }

    public class ZMQSocketServer : ISocketServer
    {

        TcpOptions _tcpOptions;

        NetMQPoller _pooler;
        NetMQQueue<NetMQMessage> _responseQueue;
        RouterSocket _serverSocket;

        BufferBlock<SocketData> _inputBuffer = new BufferBlock<SocketData>();

        ConcurrentDictionary<string, byte[]> _clientAddress = new ConcurrentDictionary<string, byte[]>();

        
        public ZMQSocketServer(IOptions<TcpOptions> tcpOptions)
        {
            this._tcpOptions = tcpOptions.Value;
        }

        public Task<SocketAddress> Listen()
        {
            _serverSocket = new RouterSocket();
            

            var bindingAddress = $"tcp://{_tcpOptions.Host}:{_tcpOptions.Port}";

            _serverSocket.Bind($"tcp://{_tcpOptions.Host}:{_tcpOptions.Port}");
            
            _serverSocket.ReceiveReady += ServerSocket_ReceiveReady;
            
            _pooler = new NetMQ.NetMQPoller();
            _responseQueue = new NetMQQueue<NetMQMessage>();
            _pooler.Add(_serverSocket);
            _pooler.Add(_responseQueue);

            _responseQueue.ReceiveReady += ResponseQueue_ReceiveReady;

            _pooler.RunAsync();

            var address = new SocketAddress()
            {
                Address = $"{_tcpOptions.Host}:{_tcpOptions.Port}",
                IsStage = true,
                Scheme = "zmq"
            };

            return Task.FromResult(address);

        }

        private void ResponseQueue_ReceiveReady(object sender, NetMQQueueEventArgs<NetMQMessage> e)
        {
            NetMQMessage m;

            while(e.Queue.TryDequeue(out m,TimeSpan.Zero))
            {
                _serverSocket.SendMultipartMessage(m);
            }
        }

        private void ServerSocket_ReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            NetMQMessage msg = new NetMQMessage();
            if (e.Socket.TryReceiveMultipartMessage(ref msg, 3))
            {
                                

                var address = msg.Pop();
                var empty = msg.Pop();
                var data = msg.Pop();

                var addressString = Convert.ToBase64String(address.Buffer);

                var messageData = new SocketData()
                {
                    Address = new SocketAddress() { Address= addressString, Scheme="zmq-client" },
                    Data = data.Buffer
                };

                _inputBuffer.Post(messageData);
            }
        }

        public Task<SocketData> Receive()
        {
            return _inputBuffer.ReceiveAsync();
        }

        public Task SendResponse(SocketAddress address, byte[] data)
        {
           
            if (address.Scheme != "zmq-client")
                return Task.CompletedTask;

            var message = new NetMQMessage();

            message.Push(data);
            message.Push(NetMQFrame.Empty);
            message.Push(Convert.FromBase64String(address.Address));



            _responseQueue.Enqueue(message);
           

            return Task.CompletedTask;           

        }
    }
}
