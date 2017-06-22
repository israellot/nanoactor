using NanoActor.Directory;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NanoActor
{
    
    public interface IRemoteSocketData
    {
        StageAddress Address { get; set; }

        byte[] Data { get; set; }
    }

    public interface IRemoteSocketManager
    {
        Task<Boolean> Send(StageAddress address, byte[] data);

        Task<StageAddress> LocalAddress();

        Task Listen();

        Task<IRemoteSocketData> Receive(TimeSpan? timeout = null, CancellationToken? ct = null);

    }

    public interface IRemoteClientSocket
    {
        Task Send(ClientAddress address, byte[] data);

        Task<ClientAddress> LocalAddress();

        Task Listen();

        Task<byte[]> Receive(TimeSpan? timeout = null, CancellationToken? ct = null);

    }

    
}
