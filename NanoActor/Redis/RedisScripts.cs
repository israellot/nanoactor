using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.Redis
{
    public class RedisScripts
    {
        public static readonly String _hashDeleteIfEqual = @"local c = redis.call('HGET', KEYS[1],ARGV[1]);
                                                             if c == ARGV[2] then redis.call('HDEL',KEYS[1],ARGV[1]) return 1 else return 0 end";

        public static readonly String _hashUpdateIfEqual = @"local c = redis.call('HGET', KEYS[1],ARGV[1]);
                                                             if c == ARGV[2] then redis.call('HSET',KEYS[1],ARGV[1],ARGV[3]) return 1 else return 0 end";


        IDatabase _database;
        public RedisScripts(IDatabase database)
        {
            _database = database;
        }


        public async Task<Boolean> HashDeleteIfEqual(string hashKey,string field, string value)
        {
            var result = await _database.ScriptEvaluateAsync(_hashDeleteIfEqual, new RedisKey[] { hashKey }, new RedisValue[] { field,value });

            return (int)result == 1;
        }

        public async Task<Boolean> HashUpdateIfEqual(string hashKey, string field, string compareValue,string updateValue)
        {
            var result = await _database.ScriptEvaluateAsync(_hashUpdateIfEqual, new RedisKey[] { hashKey }, new RedisValue[] { field, compareValue,updateValue });

            return (int)result == 1;
        }

    }
}
