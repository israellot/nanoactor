using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using DotNetty;
using DotNetty.Transport.Bootstrapping;
using DotNetty.Transport.Channels;
using DotNetty.Transport.Channels.Sockets;
using DotNetty.Codecs;
using Microsoft.Extensions.Options;
using DotNetty.Buffers;
using DotNetty.Transport.Channels.Groups;
using System.Threading.Tasks.Dataflow;
using System.Linq;
using System.Net;

namespace NanoActor
{

    public class ServerSocketHandler : ChannelHandlerAdapter
    {
        NettySocketServer server;

        IChannelGroup channels;

        public ServerSocketHandler(NettySocketServer server)
        {
            this.server = server;
        }

        public override void ChannelRegistered(IChannelHandlerContext context)
        {
            base.ChannelRegistered(context);
        }

        

        public override void ChannelActive(IChannelHandlerContext context)
        {
            
            if (channels == null)
            {
                lock (this)
                {
                    if (channels == null)
                    {
                        channels = new DefaultChannelGroup(context.Executor);
                    }
                }
            }

            channels.Add(context.Channel);


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

                server.ReceivedInput(data);

                buffer.Release();
            }

          
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception e)
        {
            Console.WriteLine("{0}", e.StackTrace);
            ctx.CloseAsync();
        }
        public override bool IsSharable => true;

        public override void ChannelReadComplete(IChannelHandlerContext ctx)
        {
            ctx.Flush();
        }

        public void SendMessage(SocketAddress address,byte[] message)
        {
            var channel = channels.FirstOrDefault(c => c.RemoteAddress.ToString() == address.Address);

            if (channel != null)
            {
                var buffer = Unpooled.WrappedBuffer(message);
                channel.WriteAndFlushAsync(buffer);

                
            }

        }

    }



    public class NettySocketServer : ISocketServer
    {

        TcpOptions _tcpOptions;
        IServiceProvider _services;

        ServerSocketHandler _handler;

        IChannel _serverChannel;
        MultithreadEventLoopGroup bossGroup = new MultithreadEventLoopGroup(1);
        MultithreadEventLoopGroup workerGroup = new MultithreadEventLoopGroup();
        ServerBootstrap bootstrap = new ServerBootstrap();

        BufferBlock<SocketData> _inputBuffer = new BufferBlock<SocketData>();

        public NettySocketServer(IServiceProvider services,IOptions<TcpOptions> tcpOptions)
        {
            _tcpOptions = tcpOptions.Value;
            _services = services;

            _handler = new ServerSocketHandler(this);

        }

        internal void ReceivedInput(SocketData data)
        {
            _inputBuffer.Post(data);
        }

        public async Task<SocketAddress> Listen()
        {



            bootstrap
               .Group(bossGroup, workerGroup)
                .Channel<TcpServerSocketChannel>()
                .Option(ChannelOption.SoBacklog, 100)
                .Option(ChannelOption.TcpNodelay, true)                
                .ChildHandler(new ActionChannelInitializer<ISocketChannel>(channel =>
                {
                    IChannelPipeline pipeline = channel.Pipeline;


                    pipeline.AddLast("framing-enc", new LengthFieldPrepender(2));
                    pipeline.AddLast("framing-dec", new LengthFieldBasedFrameDecoder(ushort.MaxValue, 0, 2, 0, 2));

                    pipeline.AddLast("handler", _handler);
                }));

            var ip = IPAddress.Parse(_tcpOptions.Host);

            _serverChannel = await bootstrap.BindAsync(ip, _tcpOptions.Port);


            var address = new SocketAddress()
            {
                Address = $"{_tcpOptions.Host}:{_tcpOptions.Port}",
                IsStage = true,
                Scheme = "netty"
            };

            return address;
        }

        public Task<SocketData> Receive()
        {
            return  _inputBuffer.ReceiveAsync();
        }

        public Task SendResponse(SocketAddress address, byte[] data)
        {
            _handler.SendMessage(address, data);

            return Task.CompletedTask;
        }
    }
}
