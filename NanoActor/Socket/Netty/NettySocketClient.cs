using DotNetty.Buffers;
using DotNetty.Codecs;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Groups;
using DotNetty.Transport.Channels.Sockets;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace NanoActor
{

    public class ClientHandler : ChannelHandlerAdapter
    {
        NettySocketClient _client;

        static IChannelGroup _channels;

        public ClientHandler(NettySocketClient client)
        {
            this._client = client;
        }

        public static IChannel GetChannel(SocketAddress address)
        {
            if (_channels == null)
                return null;

            var hostString = address.Address.Split(':')[0];
            var port = Int32.Parse(address.Address.Split(':')[1]);

            var ipAddress = IPAddress.Parse(hostString);
            IPEndPoint ipv4Endpoint;
            IPEndPoint ipv6Endpoint;
            if (ipAddress.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
            {
                ipv4Endpoint = new IPEndPoint(ipAddress, port);
                ipv6Endpoint = new IPEndPoint(ipAddress.MapToIPv6(), port);
            }
            else
            {
                ipv6Endpoint = new IPEndPoint(ipAddress.MapToIPv6(), port);
                ipv4Endpoint = new IPEndPoint(ipAddress.MapToIPv4(), port);
                
            }

          
            var channel = _channels.FirstOrDefault(c =>  c.RemoteAddress.ToString() == ipv4Endpoint.ToString() || c.RemoteAddress.ToString()==ipv6Endpoint.ToString());

            return channel;
            

        }

        public override void ChannelActive(IChannelHandlerContext context)
        {
            if (_channels == null)
            {
                lock (this)
                {
                    if (_channels == null)
                    {
                        _channels = new DefaultChannelGroup(context.Executor);
                    }
                }
            }

            _channels.Add(context.Channel);
            base.ChannelActive(context);
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object message)
        {
            var buffer = message as IByteBuffer;
            if (buffer != null)
            {
                var data = new SocketData()
                {
                    Address = new SocketAddress() { Address = ctx.Channel.RemoteAddress.ToString(), Scheme = "netty" },
                    Data = buffer.ToArray()
                };

                _client.ReceivedInput(data);

                buffer.Release();
            }

            
        }

        

        public override void ChannelReadComplete(IChannelHandlerContext context) => context.Flush();

        

        public override void ExceptionCaught(IChannelHandlerContext context, Exception e)
        {
            Console.WriteLine(DateTime.Now.Millisecond);
            Console.WriteLine(e.StackTrace);
            context.CloseAsync();
        }
    }

    public class NettySocketClient : ISocketClient
    {
        TcpOptions _tcpOptions;
        IServiceProvider _services;

        
        BufferBlock<SocketData> _inputBuffer = new BufferBlock<SocketData>();
                       

        MultithreadEventLoopGroup _enventGroup = new MultithreadEventLoopGroup();

        

        public NettySocketClient(IServiceProvider services, IOptions<TcpOptions> tcpOptions)
        {
            _tcpOptions = tcpOptions.Value;
            _services = services;

           

        }

        internal void ReceivedInput(SocketData data)
        {
            _inputBuffer.Post(data);
        }

        public Task<SocketData> Receive()
        {
            return _inputBuffer.ReceiveAsync();
        }


        static SemaphoreSlim _channelShamaphore = new SemaphoreSlim(1, 1);
        private async Task<IChannel> GetOrCreateChannel(SocketAddress address)
        {

            var channel = ClientHandler.GetChannel(address);

            if (channel == null)
            {
                await _channelShamaphore.WaitAsync();

                channel = ClientHandler.GetChannel(address);

                if (channel == null)
                {
                    try
                    {
                        var bootstrap = new Bootstrap();
                        bootstrap
                            .Group(_enventGroup)
                            .Channel<TcpSocketChannel>()
                            .Option(ChannelOption.TcpNodelay, true)
                            .Handler(new ActionChannelInitializer<ISocketChannel>(c =>
                            {
                                IChannelPipeline pipeline = c.Pipeline;


                                pipeline.AddLast("framing-enc", new LengthFieldPrepender(2));
                                pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder(ushort.MaxValue, 0, 2, 0, 2));
                                pipeline.AddLast("handler", new ClientHandler(this));


                            }));

                        var host = address.Address.Split(':')[0];
                        var port = Int32.Parse(address.Address.Split(':')[1]);


                        var ip = IPAddress.Parse(host);

                        IChannel bootstrapChannel = await bootstrap.ConnectAsync(ip, port);
                                                                        
                        channel = bootstrapChannel;
                    }
                    finally
                    {
                        _channelShamaphore.Release();
                    }
                }

            }

           

            return channel;
        }

        public async Task SendRequest(SocketAddress address, byte[] data)
        {
            if (address == null || address.Address == null)
            {
                address = new SocketAddress()
                {
                    Address = $"{_tcpOptions.Host}:{_tcpOptions.Port}",
                    IsStage = true,
                    Scheme = "netty"
                };
            }

            var channel = await this.GetOrCreateChannel(address);

            var buffer = Unpooled.WrappedBuffer(data);

            await channel.WriteAndFlushAsync(buffer);

            ;
            
        }
    }
}
