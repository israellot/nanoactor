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
       
    public class RedisStageDirectory : IStageDirectory
    {
        RedisConnectionFactory _connectionFactory;

        NanoServiceOptions _serviceOptions;

        Lazy<IDatabase> _databaseLazy;

        ITransportSerializer _serializer;

        ILogger<RedisStageDirectory> _logger;

        IMemoryCache _memoryCache;

        IDatabase _database => _databaseLazy.Value;

        String _stageDirectoryKey = "stage-directory";

        Task _updateTask;

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

            _updateTask = Task.Run(async () => {
                while (true)
                {
                    try
                    {
                        await GetAllStages(true);
                        await Task.Delay(2);
                    }
                    catch{ }
                }
                
            });
        }

        public async Task<Boolean> RegisterStage(string stageId)
        {

            try
            {
                await _database.SetAddAsync(_stageDirectoryKey, stageId);
            }catch(RedisException e)
            {
                await _database.KeyDeleteAsync(_stageDirectoryKey);
                await _database.SetAddAsync(_stageDirectoryKey, stageId);
            }

            return true;
        }

        public async Task<List<string>> GetAllStages(Boolean forceUpdate=false)
        {
            List<string> stages;

            if (!forceUpdate && _memoryCache.TryGetValue("all",out stages))
            {
                return stages;
            }
            else
            {
                var stagesValues = await _database.SetMembersAsync(_stageDirectoryKey);

                stages= stagesValues.Select(s => (string)s).ToList();

                _memoryCache.Set("all", stages,TimeSpan.FromSeconds(4));

                return stages;
            }
           
        }

        public async Task UnregisterStage(string stageId)
        {
            await _database.SetRemoveAsync(_stageDirectoryKey, stageId);
        }

        public async Task<bool> IsLive(string stageId)
        {
            var allStages = await this.GetAllStages();
            return allStages.Contains(stageId);
        }
    }
}
