using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor
{
    public class MemoryStageDirectory : IStageDirectory
    {
        ConcurrentDictionary<string, StageAddress> _stageDictionary = new ConcurrentDictionary<string, StageAddress>();

        public Task Clear(string stageId)
        {
            throw new NotImplementedException();
        }

        public async Task<List<StageAddress>> GetAllStageAddresses(string stageId)
        {
            var address = await GetStageAddress(stageId);

            return new List<StageAddress>() { address };
        }

        public Task<List<string>> GetAllStages()
        {
            throw new NotImplementedException();
        }

        public Task<StageAddress> GetStageAddress(string stageId)
        {
            _stageDictionary.TryGetValue(stageId, out var address);
                        
            return Task.FromResult(address);
        }

        ConcurrentDictionary<string, long> _lastPing = new ConcurrentDictionary<string, long>();

        public Task<Boolean> IsLive(string stageId)
        {
            var lastPing = _lastPing.GetOrAdd(stageId, 0);

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return Task.FromResult(now - lastPing < 60);
        }

        

        public Task Ping(string stageId)
        {
            var lastPing = _lastPing.GetOrAdd(stageId, 0);

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (now - lastPing > 30)
            {
                _lastPing.AddOrUpdate(stageId, now, (s, ping) => now);
            }

            return Task.CompletedTask;

        }
        public Task<StageAddress> RegisterStage(string stageId,SocketAddress address)
        {
            var newAddress = _stageDictionary.AddOrUpdate(
                stageId,
                new StageAddress() { SocketAddress = address, StageId = stageId },
                (a, s) => { s.SocketAddress = address; return s; }
                );

            return Task.FromResult(newAddress);
        }

        public Task UnregisterStage(string stageId)
        {
            _stageDictionary.TryRemove(stageId, out _);

            return Task.CompletedTask;
        }
    }
}
