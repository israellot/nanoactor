using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor
{
    public interface ISocketClient
    {
        Task SendRequest(string stageId, byte[] data);

        Task<SocketData> Receive();
    }

}
