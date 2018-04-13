using NanoActor.Directory;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NanoActor
{
    public class DataReceivedEventArgs : EventArgs
    {
        public SocketData SocketData { get; set; }
    }

    public interface ISocketServer
    {
        Task<Boolean> Listen();
        Task SendResponse(string stageId, byte[] data);

        event EventHandler<DataReceivedEventArgs> DataReceived;

    }

    
    
}
