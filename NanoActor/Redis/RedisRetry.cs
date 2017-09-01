using Polly;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace NanoActor.Redis
{
    public class RedisRetry
    {
        public static Type[] RetryableExceptions = new[]
        {
            typeof(TimeoutException),
            typeof(RedisTimeoutException),
            typeof(RedisException),
            typeof(RedisServerException)
        };

        public static Policy RetryPolicy = Policy.Handle<Exception>(ex =>
        {
            return RetryableExceptions.Any(t => t == ex.GetType());
        })
        .WaitAndRetry(new[] {
            TimeSpan.FromMilliseconds(10),
            TimeSpan.FromMilliseconds(20),
            TimeSpan.FromMilliseconds(50),
            TimeSpan.FromMilliseconds(100),
            TimeSpan.FromMilliseconds(1000) });




    }
}
