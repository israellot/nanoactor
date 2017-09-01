using NanoActor.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using StackExchange.Redis.KeyspaceIsolation;
using Microsoft.Extensions.Options;
using NanoActor.Options;
using StackExchange.Redis;
using System.Linq;
using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace NanoActor
{

    public class RedisStageAddress
    {
        public String StageGuid { get; set; }

        public Int32 LastPing { get; set; }

        public Boolean Live { get; set; }

        public override string ToString()
        {
            return string.Join(",", this.StageGuid, this.LastPing, this.Live?"1":"0");
        }

        public RedisStageAddress() { }

        public RedisStageAddress(string s)
        {
            var split = s.Split(',');

            StageGuid = split[0];
            LastPing = Int32.Parse(split[1]);
            Live = split[2] == "1";
        }

    }

    public class RedisStageDirectory : IStageDirectory
    {
        RedisConnectionFactory _connectionFactory;

        NanoServiceOptions _serviceOptions;

        Lazy<IDatabase> _databaseLazy;

        ITransportSerializer _serializer;

        ILogger<RedisStageDirectory> _logger;

        IMemoryCache _memoryCache;

        IDatabase _database => _databaseLazy.Value;

        public RedisStageDirectory(RedisConnectionFactory connectionFactory,ILogger<RedisStageDirectory> logger, IOptions<NanoServiceOptions> serviceOptions,ITransportSerializer serializer)
        {
            _connectionFactory = connectionFactory;
            _serviceOptions = serviceOptions.Value;
            _serializer = serializer;
            _logger = logger;

            var cacheOptions = Microsoft.Extensions.Options.Options.Create<MemoryCacheOptions>(new MemoryCacheOptions() {  });
            
            _memoryCache = new MemoryCache(cacheOptions);

            _databaseLazy = new Lazy<IDatabase>(() => {
                return _connectionFactory.GetDatabase().WithKeyPrefix($"{_serviceOptions.ServiceName}");
            });
        }
        
        public async Task<StageAddress> GetStageAddress(string stageId)
        {
            StageAddress stageAddress;
            if (!_memoryCache.TryGetValue(stageId,out stageAddress))
            {
                var address = await _database.HashGetAsync($"stage-directory", stageId);

                stageAddress = _serializer.Deserialize<StageAddress>(address);

                if(stageAddress != null)
                    _memoryCache.Set(stageId, stageAddress,TimeSpan.FromSeconds(4));
            }
            else
            {

            }

            return stageAddress;
        }

        public async Task<StageAddress> RegisterStage(string stageId, SocketAddress address)
        {
            var stageAddress = new StageAddress()
            {
                SocketAddress = address,
                StageId = stageId
            };

            //var redisAddress = new RedisStageAddress() { StageGuid = stageId, Live = true, LastPing=datet };

            await _database.HashSetAsync($"stage-directory", stageId, _serializer.Serialize(stageAddress));

            return stageAddress;
        }

        public async Task<List<string>> GetAllStages()
        {
            List<string> stages;

            if (_memoryCache.TryGetValue("all",out stages))
            {
                return stages;
            }
            else
            {
                var stagesValues = await _database.HashKeysAsync($"stage-directory");

                stages= stagesValues.Select(s => (string)s).ToList();

                _memoryCache.Set("all", stages,TimeSpan.FromSeconds(4));

                return stages;
            }
           
        }

        public async Task UnregisterStage(string stageId)
        {
            await _database.HashDeleteAsync("stage-directory", stageId);
        }
    }
}
