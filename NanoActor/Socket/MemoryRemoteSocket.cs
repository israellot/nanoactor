using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using NanoActor.Directory;

namespace NanoActor
{

    public class MemoryRemoteSocketData : IRemoteSocketData
    {
        public StageAddress Address { get; set; }
        public byte[] Data { get; set; }
    }

    public class MemoryRemoteSocket : IRemoteSocketManager
    {
        BufferBlock<byte[]> _socketBuffer =  new BufferBlock<byte[]>();

        public async Task Listen()
        {
            
        }

        public async Task<StageAddress> LocalAddress()
        {
            return new StageAddress() { IsLocal = true };
        }

        public async Task<IRemoteSocketData> Receive(TimeSpan? timeout = null, CancellationToken? ct = null)
        {
            var data = await _socketBuffer.ReceiveAsync(timeout ?? TimeSpan.FromMilliseconds(-1), ct ?? CancellationToken.None);
            if (data != null)
            {
                return new MemoryRemoteSocketData()
                {
                    Address = new StageAddress() { IsLocal = true },
                    Data = data
                };

            }
            else
            {
                return null;
            }             
        }

        public Task<Boolean> Send(StageAddress address, byte[] data)
        {
            if (address.IsLocal)
            {
                _socketBuffer.Post(data);
            } 
            else
            {
                throw new ArgumentException("Address should be local", "address");
            }

            return Task.FromResult(true);
                
        }
    }
}
