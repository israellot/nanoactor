using NanoActor.Directory;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NanoActor
{
           
        
    public interface ISocketServer
    {
        Task<SocketAddress> Listen();
        Task<SocketData> Receive();
        Task SendResponse(SocketAddress address, byte[] data);

    }

    
    
}
