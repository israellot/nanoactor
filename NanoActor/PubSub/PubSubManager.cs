using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace NanoActor.PubSub
{
    public class PubSubSubscription
    {
        public String Channel { get; set; }

        public SubscriptionHandler Handler { get; set; }
    }

    public class PubSubManager
    {
        IServiceProvider _services;

        IPubSub _pubsub;
        ITransportSerializer _serializer;



        ConcurrentDictionary<string, PubSubSubscription> _subscriptions = new ConcurrentDictionary<string, PubSubSubscription>();

        String ActorEventChannel(string actorInterface, string eventName, string actorId)
        {
            if (string.IsNullOrEmpty(actorId))
                actorId = "*";

            var channel = $"{actorInterface}:{eventName}:{actorId}";

            return channel;
        }

        public PubSubManager(IServiceProvider services,ITransportSerializer serializer, IPubSub pubsub)
        {
            _services = services;
            _serializer = serializer;
            _pubsub = pubsub;
        }

        public Task Publish<T>(string actorInterface, string eventName, string actorId,T data)
        {
            var e = new ActorEvent<T>()
            {
                ActorId = actorId,
                ActorInterface = actorInterface,
                EventData = data,
                EventName = eventName
            };

            return _pubsub.Publish(ActorEventChannel(e.ActorInterface,e.EventName,e.ActorId), _serializer.Serialize<T>(data));
        }

        public async Task<string> Watch<T>(string actorInterface,string eventName,string actorId,Action<ActorEvent<T>> handler)
        {
            var channel = ActorEventChannel(actorInterface, eventName, actorId);
                        
            SubscriptionHandler action = (string c, ref byte[] data) => {

                var e = new ActorEvent<T>()
                {
                    ActorInterface = actorInterface,
                    EventName = eventName,
                    EventData = _serializer.Deserialize<T>(data),
                    ActorId = c.Split(':').Last()
                };

                handler(e);

            };

            var guid = Guid.NewGuid().ToString();

            _subscriptions[guid] = new PubSubSubscription() { Channel = channel, Handler = action };

            await _pubsub.Subscribe(channel, action);

            return guid;

        }

       
        public async Task Cancel(string subscriptionId)
        {
            if(_subscriptions.TryRemove(subscriptionId, out var subscription))
            {
                await _pubsub.Unsubscribe(subscription.Channel, subscription.Handler);
            }            
        }
    }

 
}
