using Microsoft.Extensions.Options;
using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace NanoActor
{
    public class ZMQSocketClient : ISocketClient
    {

        TcpOptions _tcpOptions;

        ConcurrentDictionary<string, Tuple<DealerSocket, NetMQQueue<Tuple<DealerSocket, NetMQMessage>>>> _sockets = new ConcurrentDictionary<string, Tuple<DealerSocket, NetMQQueue<Tuple<DealerSocket, NetMQMessage>>>>();

        BufferBlock<SocketData> _inputBuffer = new BufferBlock<SocketData>();

        NetMQPoller _pooler;

        public ZMQSocketClient(IOptions<TcpOptions> tcpOptions)
        {
            this._tcpOptions = tcpOptions.Value;

            _pooler = new NetMQPoller();

            _pooler.RunAsync();
        }

        public Task<SocketData> Receive()
        {
            return _inputBuffer.ReceiveAsync();
        }

        static SemaphoreSlim _socketConnectSemaphore = new SemaphoreSlim(1, 1);
        public async Task SendRequest(SocketAddress address, byte[] data)
        {

            if (address == null || address.Address == null)
            {
                address = new SocketAddress()
                {
                    Address = $"{_tcpOptions.Host}:{_tcpOptions.Port}",
                    IsStage = true,
                    Scheme = "zmq"
                };
            }


            if (!_sockets.TryGetValue(address.Address, out var tuple))
            {
                await _socketConnectSemaphore.WaitAsync();

                if (!_sockets.TryGetValue(address.Address, out tuple))
                {
                    try
                    {
                        var socket = new DealerSocket();
                        var queue = new NetMQ.NetMQQueue<Tuple<DealerSocket, NetMQMessage>>();
                        tuple = new Tuple<DealerSocket, NetMQQueue<Tuple<DealerSocket, NetMQMessage>>>(socket, queue);

                        

                        socket.Connect($"tcp://{address.Address}");

                        queue.ReceiveReady += Queue_ReceiveReady;

                        socket.ReceiveReady += Socket_ReceiveReady;
                        _pooler.Add(socket);
                        _pooler.Add(queue);
                        

                        _sockets.TryAdd(address.Address, tuple);
                    }
                    catch(Exception ex)
                    {
                        tuple = null;
                    }
                    finally
                    {
                        _socketConnectSemaphore.Release();

                    }
                }

                if (tuple == null)
                {
                    //failed to connect
                    throw new Exception("Failed to Connect to socket");
                }
            }

            var message = new NetMQMessage();

            
            message.Push(data);
            message.Push(NetMQFrame.Empty);
            // message.Push(Guid.NewGuid().ToByteArray());

            tuple.Item2.Enqueue(new Tuple<DealerSocket, NetMQMessage>(tuple.Item1, message));

        }

        private void Queue_ReceiveReady(object sender, NetMQQueueEventArgs<Tuple<DealerSocket,NetMQMessage>> e)
        {
            Tuple<DealerSocket, NetMQMessage> queueItem;

            while(e.Queue.TryDequeue(out queueItem, TimeSpan.Zero))
            {
                queueItem.Item1.SendMultipartMessage(queueItem.Item2);
            }
        }

        private void Socket_ReceiveReady(object sender, NetMQ.NetMQSocketEventArgs e)
        {
            NetMQMessage msg = new NetMQMessage();
            if (e.Socket.TryReceiveMultipartMessage(ref msg,2))
            {
                //var empty = msg.Pop();
                var data = msg.Last;

                var messageData = new SocketData()
                {
                    Address = new SocketAddress() { Address = e.Socket.Options.LastEndpoint, Scheme = "zmq" },
                    Data = data.Buffer
                };

                _inputBuffer.Post(messageData);
            }
        }

    }
}

        
    
