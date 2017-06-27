using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NanoActor.Directory;
using System.Net.Sockets;
using System.Net;
using System.Collections.Concurrent;
using System.Threading.Tasks.Dataflow;
using System.Linq;
using Microsoft.Extensions.Options;

namespace NanoActor
{
    public class TcpAddress: StageAddress
    {

    }

    public class TcpOptions
    {
        public int Port { get; set; } = 34567;

        public string Host { get; set; } = "0.0.0.0";

        public int MaxClientSockets { get; set; } = 3;
    }


    public class TcpRemoteSocket : IRemoteSocketManager
    {
        TcpListener listerner;

        IServiceProvider _services;

        TcpOptions _options;

        BufferBlock<IRemoteSocketData> _messagesBuffer;

        Boolean _isListening;

        ConcurrentDictionary<string, TcpClient> _inSockets = new ConcurrentDictionary<string, TcpClient>();

        ConcurrentDictionary<string, List<TcpClient>> _outSockets = new ConcurrentDictionary<string, List<TcpClient>>();

        public TcpRemoteSocket(IServiceProvider services,IOptions<TcpOptions> options)
        {
            _services = services;

            _options = options.Value ?? new TcpOptions();

            _messagesBuffer = new BufferBlock<IRemoteSocketData>();
        }

        
        
        protected StageAddress GetStageAddress(TcpClient client)
        {
            return new TcpAddress()
            {
                IsLocal = false,
                Address = $"tcp://{ client.Client.RemoteEndPoint.ToString() }"
            };
        }

        protected async Task<byte[]> ReadNBytes(TcpClient tcpClient,int count)
        {
            var buffer  = new byte[count];

            var totalRead = 0;

            while (totalRead< count && tcpClient.Client.Connected)
            {
                //var segment = new ArraySegment<byte>(buffer,totalRead, count - totalRead);

                //var read = await tcpClient.Client.ReceiveAsync(segment, SocketFlags.None);

                var read = await tcpClient.GetStream().ReadAsync(buffer, totalRead, count- totalRead);
                totalRead += read;

                if (read == 0)
                    return null;
            }

            return buffer;
        }

        protected async Task<byte[]> ReceiveMessage(TcpClient tcpClient)
        {
            
            //read header
            var header = await ReadNBytes(tcpClient, 4);

            if (header == null)
                return null;

            var messageSize = BitConverter.ToInt32(header, 0);

            //read message
            var message = await ReadNBytes(tcpClient, messageSize);

            return message;
        }

        public Task Listen()
        {
            _isListening = true;
            try
            {
                listerner = new TcpListener(IPAddress.Any, _options.Port);

                listerner.Start();

                Task.Run(async () =>
                {
                    while (true)
                    {
                        try
                        {
                            var tcpClient = await listerner.AcceptTcpClientAsync();

                            var clientAddress = GetStageAddress(tcpClient);
                            _inSockets.TryAdd(clientAddress.Address, tcpClient);

                            
                            Task.Run(async () => {
                                //new client accepted
                                await ProcessSocket(tcpClient);

                                _inSockets.TryRemove(clientAddress.Address, out _);
                            });
                        }
                        catch(Exception ex)
                        {

                        }
                        
                    }


                }).ConfigureAwait(false);

                
            }
            catch(Exception ex)
            {

            }

            return Task.FromResult(0);

        }

        public async Task ProcessSocket(TcpClient tcpClient,int maxMessages=-1)
        {
            
            var clientAddress = GetStageAddress(tcpClient);

            tcpClient.NoDelay = true;
                       

            try
            {
                

                while (tcpClient.Connected)
                {
                    var message = await ReceiveMessage(tcpClient);


                    var messageData = new MemoryRemoteSocketData()
                    {
                        Address = clientAddress,
                        Data = message
                    };

                    _messagesBuffer.Post(messageData);


                    if (message == null)
                    {
                        break;
                    }

                    if (maxMessages > 0)
                    {
                        maxMessages--;

                        if (maxMessages == 0)
                            break;
                    }
                }

                tcpClient.Dispose();

            }
            catch (Exception ex)
            {

            }
        }

        public static async Task<string> GetLocalIP()
        {
            string ipv4Address = String.Empty;

            foreach (IPAddress currentIPAddress in await Dns.GetHostAddressesAsync(Dns.GetHostName()))
            {
                if (currentIPAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                {
                    ipv4Address = currentIPAddress.ToString();
                    break;
                }
            }

            return ipv4Address;
        }

        public async Task<StageAddress> LocalAddress()
        {
            //var hostname = Dns.GetHostName();

            //var ip = await GetLocalIP();

            return new TcpAddress()
            {
                Address = $"tcp://{_options.Host}:{_options.Port}"
            };
        }

        public async Task<IRemoteSocketData> Receive(TimeSpan? timeout = default(TimeSpan?), CancellationToken? ct = default(CancellationToken?))
        {
            return await _messagesBuffer.ReceiveAsync(timeout ?? TimeSpan.FromMilliseconds(-1), ct ?? CancellationToken.None);                        
        }

        protected async Task<int> SendSocketMessage(TcpClient tcpClient, byte[] data)
        {
            var messageSize = data.Length;
            var header = BitConverter.GetBytes(messageSize);
            var endMessage = header.Concat(data).ToArray();
            var sent = await tcpClient.Client.SendAsync(new ArraySegment<byte>(endMessage), SocketFlags.None);

            return sent;
        }

        

        protected void ConnectTcpClient(TcpClient tcpClient, StageAddress address)
        {
            if (!tcpClient.Connected)
            {
                lock (tcpClient)
                {
                    if (!tcpClient.Connected)
                    {

                        //out socket
                        tcpClient.NoDelay = true;
                        tcpClient.SendTimeout = 5;
                        tcpClient.ReceiveTimeout = 5;

                        var hostPort = address.Address.Split(new[] { "tcp://" }, StringSplitOptions.None)[1];
                        var host = hostPort.Split(':')[0];

                        var port = Int32.Parse(hostPort.Split(':')[1]);

                        tcpClient.ConnectAsync(host, port).Wait();

                        Task.Run(async () => {
                            await ProcessSocket(tcpClient);

                            if(_outSockets.TryGetValue(address.Address,out var tcpClientList)){
                                tcpClientList.Remove(tcpClient);
                            } 
                           
                        });

                    }
                }
            }
        }

        public async Task<Boolean> Send(StageAddress address, byte[] data)
        {
            if (address.Address == null)
                address = await LocalAddress();

            TcpClient tcpClient;

            if (_isListening)
            {
                //on stage
                if (_inSockets.TryGetValue(address.Address, out tcpClient))
                {
                   
                    return await SendSocketMessage(tcpClient, data) > 0;

                }

            }


            //on client
            var tcpClientList = _outSockets.GetOrAdd(address.Address, new List<TcpClient>());

            if (tcpClientList.Count < _options.MaxClientSockets)
            {
                tcpClient = new TcpClient();
                lock (tcpClientList)
                {
                    tcpClientList.Add(tcpClient);
                }
            }
            else
            {
                tcpClient = tcpClientList.OrderBy(c => Guid.NewGuid()).First();
            }

            ConnectTcpClient(tcpClient, address);


            var sent = await SendSocketMessage(tcpClient, data);
            return sent > 0;

           
        }
    }
}
