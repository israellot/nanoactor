using Microsoft.Extensions.Options;
using NanoActor.Redis;
using NanoActor.Telemetry;
using Polly;
using StackExchange.Redis;
using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.PubSub
{
    public class RedisPubSub : IPubSub
    {
        IServiceProvider _services;
              
        RedisConnectionFactory _connectionFactory;
                
        Lazy<ISubscriber> _subscriberLazy;

        ITelemetry _telemetry;

        ISubscriber _subscriber => _subscriberLazy.Value;

        Policy _redisPolicy = RedisRetry.RetryPolicy;

        ConcurrentDictionary<SubscriptionHandler, Action<RedisChannel, RedisValue>> _actionMap = new ConcurrentDictionary<SubscriptionHandler, Action<RedisChannel, RedisValue>>();

        public RedisPubSub(IServiceProvider services,RedisConnectionFactory connectionFactory, ITelemetry telemetry)
        {
            _services = services;
            _telemetry = telemetry;

            _connectionFactory = connectionFactory;

            _subscriberLazy = new Lazy<ISubscriber>(() => {

                _connectionFactory.GetConnection().PreserveAsyncOrder = false;

                return _connectionFactory.GetSubscriber();
            });

        }

        public Task Publish(string channel, byte[] data)
        {
            
            try
            {
                _redisPolicy.Execute(() => {
                    _subscriber.Publish(channel, data);
                });
            }
            catch(Exception ex)
            {
                _telemetry.Exception(ex);
                throw;
            }

            return Task.CompletedTask;
        }
        
        public Task Subscribe(string channel, SubscriptionHandler handler)
        {            
            var action = new Action<RedisChannel, RedisValue>((c, v) => {                
                byte[] bytes = (byte[])v;
                handler(c,ref bytes);   
            });

            _actionMap.AddOrUpdate(handler, action,(a,b) => action);

            try
            {
                _redisPolicy.Execute(() => {
                    _subscriber.Subscribe(channel, action);                    
                });
            }
            catch (Exception ex)
            {
                _telemetry.Exception(ex);
                throw;
            }

            return Task.CompletedTask;
        }

        public Task Unsubscribe(string channel, SubscriptionHandler handler)
        {
            if(_actionMap.TryRemove(handler,out var action))
            {
                try
                {
                    _redisPolicy.Execute(() => {
                        _subscriber.Unsubscribe(channel, action);
                    });
                }
                catch (Exception ex)
                {
                    _telemetry.Exception(ex);
                    throw;
                }
            }

            return Task.CompletedTask;
        }
    }
}
