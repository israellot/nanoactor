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
        Task<Boolean> Listen();
        Task<SocketData> Receive();
        Task SendResponse(string stageId, byte[] data);

        Int32 InboundBacklogCount();

    }

    
    
}
