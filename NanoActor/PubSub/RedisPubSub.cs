using Microsoft.Extensions.Options;
using NanoActor.Redis;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.PubSub
{
    public class RedisPubSub : IPubSub
    {
        IServiceProvider _services;
              
        RedisConnectionFactory _connectionFactory;
                
        Lazy<ISubscriber> _subscriberLazy;

        ISubscriber _subscriber => _subscriberLazy.Value;

        Dictionary<Action<string, byte[]>, Action<RedisChannel, RedisValue>> _actionMap = new Dictionary<Action<string, byte[]>, Action<RedisChannel, RedisValue>>();

        public RedisPubSub(IServiceProvider services,RedisConnectionFactory connectionFactory)
        {
            _services = services;
           
            _connectionFactory = connectionFactory;

            _subscriberLazy = new Lazy<ISubscriber>(() => {
                return _connectionFactory.GetSubscriber();
            });

        }

        public Task Publish(string channel, byte[] data)
        {
            _subscriber.Publish(channel, data);

            

            return Task.CompletedTask;
        }

        public Task Subscribe(string channel, Action<string,byte[]> handler)
        {
            var action = new Action<RedisChannel, RedisValue>((c, v) => {
                handler(c, v);
            });

            _actionMap[handler]= action;

            _subscriber.Subscribe(channel, action);

            

            return Task.CompletedTask;
        }

        public Task Unsubscribe(string channel, Action<string, byte[]> handler)
        {
            if(_actionMap.TryGetValue(handler,out var action))
            {
                _subscriber.Unsubscribe(channel, action);
            }

            return Task.CompletedTask;
        }
    }
}
