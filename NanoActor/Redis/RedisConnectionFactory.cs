using Microsoft.Extensions.Options;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor.Redis
{
    public class RedisConnectionFactory
    {

        RedisOptions _redisOptions;

        Lazy<IConnectionMultiplexer> _multiplexerLazy;

        IConnectionMultiplexer _multiplexer => _multiplexerLazy.Value;

        Lazy<ISubscriber> _subscriberLazy;
        ISubscriber _subscriber => _subscriberLazy.Value;

        public RedisConnectionFactory(IOptions<RedisOptions> redisOptions)
        {
            _redisOptions = redisOptions.Value;

            _multiplexerLazy = new Lazy<IConnectionMultiplexer>(() => {

                var connectionOptions = ConfigurationOptions.Parse(_redisOptions.ConnectionString);

                connectionOptions.AbortOnConnectFail = false;
                connectionOptions.ConnectRetry = Int32.MaxValue;
                connectionOptions.ResolveDns = true;

                var m = ConnectionMultiplexer.Connect(_redisOptions.ConnectionString);
                               

                return m;
            });

            _subscriberLazy = new Lazy<ISubscriber>(() =>
            {
                return _multiplexerLazy.Value.GetSubscriber();
            });
        }

        public IConnectionMultiplexer GetConnection()
        {
            return _multiplexer;
        }

        public IDatabase GetDatabase(Int32 databaseNumber = -1)
        {

            if (databaseNumber == -1)
                databaseNumber = _redisOptions.Database;

            return _multiplexer.GetDatabase(databaseNumber);
        }

        public ISubscriber GetSubscriber()
        {
            return _multiplexer.GetSubscriber();
        }
    }
}
