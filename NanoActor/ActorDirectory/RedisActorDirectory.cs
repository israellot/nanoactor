using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using NanoActor.Directory;
using NanoActor.Options;
using NanoActor.Redis;
using StackExchange.Redis;
using StackExchange.Redis.KeyspaceIsolation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor
{
    public class RedisActorDirectory: IActorDirectory
    {

        RedisConnectionFactory _connectionFactory;

        NanoServiceOptions _serviceOptions;

        Lazy<IDatabase> _databaseLazy;

        ITransportSerializer _serializer;

        IStageDirectory _stageDirectory;

        IMemoryCache _memoryCache;

        IDatabase _database => _databaseLazy.Value;

        Lazy<RedisScripts> _scriptsLazy;
        RedisScripts _scripts => _scriptsLazy.Value;

        String StageAddress(string actorId) => $"actor:{actorId}";

        public RedisActorDirectory(RedisConnectionFactory connectionFactory, IStageDirectory stageDirectory, IOptions<NanoServiceOptions> serviceOptions, ITransportSerializer serializer)
        {
            _connectionFactory = connectionFactory;
            _serviceOptions = serviceOptions.Value;
            _serializer = serializer;
            _stageDirectory = stageDirectory;

            var cacheOptions = Microsoft.Extensions.Options.Options.Create<MemoryCacheOptions>(new MemoryCacheOptions() { CompactOnMemoryPressure = false });

            _memoryCache = new MemoryCache(cacheOptions);

            _databaseLazy = new Lazy<IDatabase>(() => {
                return _connectionFactory.GetDatabase().WithKeyPrefix($"{_serviceOptions.ServiceName}");
            });

            _scriptsLazy = new Lazy<RedisScripts>(() => { return new RedisScripts(_database); });
        }
                        
        public async Task<StageAddressQueryResponse> GetAddress(string actorTypeName, string actorId)
        {
            var key = string.Join(":", actorTypeName, actorId);

            StageAddressQueryResponse address;

            if (_memoryCache.TryGetValue(key, out address))
            {
                return address;
            }
            else
            {
                var stageId = await _database.HashGetAsync("actor-directory",key, CommandFlags.HighPriority);

                if (stageId == RedisValue.Null)
                {
                    var stages = await _stageDirectory.GetAllStages();
                    stageId = stages.OrderBy(s => Guid.NewGuid()).FirstOrDefault();

                    if (stageId == RedisValue.Null)
                        throw new Exception("No live stage to connect to");

                    var updated = await _database.HashSetAsync("actor-directory", key, stageId, When.NotExists);

                    if (!updated)
                    {
                        stageId = await _database.HashGetAsync("actor-directory", key);
                    }
                }

                address = new StageAddressQueryResponse() { Found = true, StageId = stageId };

                _memoryCache.Set(key, address, TimeSpan.FromSeconds(5));

                return address;
            }

            
        }

        public async Task Refresh(string actorTypeName, string actorId)
        {
            var key = string.Join(":", actorTypeName, actorId);
            _memoryCache.Remove(key);
        }

        public async Task<string> Reallocate(string actorTypeName,string actorId,string oldStageId)
        {
            var stageId = RedisValue.Null;

            var key = string.Join(":", actorTypeName, actorId);

            var stages = await _stageDirectory.GetAllStages();
            stageId = stages.OrderBy(s => Guid.NewGuid()).FirstOrDefault();
           
            if (stageId == RedisValue.Null)
                throw new Exception("No live stage to connect to");

            var updated = await _scripts.HashUpdateIfEqual("actor-directory", key, oldStageId, stageId);

            await Refresh(actorTypeName, actorId);

            if (updated)
                return stageId;
            else
                return await _database.HashGetAsync("actor-directory", key);

        }

        public async Task RegisterActor(string actorTypeName, string actorId, string stageId)
        {
            await _database.HashSetAsync("actor-directory", string.Join(":", actorTypeName, actorId), stageId);            
        }
            
        public async Task UnregisterActor(string actorTypeName,string actorId, string stageId)
        {
            var deleted = await _scripts.HashDeleteIfEqual("actor-directory", string.Join(":", actorTypeName, actorId), stageId);
                        
        }
    }
}
