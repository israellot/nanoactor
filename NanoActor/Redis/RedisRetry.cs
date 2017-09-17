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
       

        public static Policy RetryPolicy = Policy
            .Handle<TimeoutException>()
            .Or<RedisTimeoutException>()
            .Or<RedisException>()
            .Or<RedisServerException>()
            .WaitAndRetry(new[] {
                TimeSpan.FromMilliseconds(10),
                TimeSpan.FromMilliseconds(20),
                TimeSpan.FromMilliseconds(50),
                TimeSpan.FromMilliseconds(100),
                TimeSpan.FromMilliseconds(1000) });




    }
}
