using NanoActor.PubSub;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.ActorProxy
{
    public class ActorEventProxy:IDisposable
    {
        PubSubManager _pubsub;

        string _actorId;
        string _actorInterface;

        HashSet<string> _subscriptions = new HashSet<string>();

        public ActorEventProxy(PubSubManager pubsub, string actorInterface,string actorId=null)
        {
            _pubsub = pubsub;
            _actorInterface = actorInterface;
            _actorId = actorId;
        }

        public async Task Watch<T>(string eventName,Action<ActorEvent<T>> handler)
        {

            var subscription = await _pubsub.Watch(_actorInterface, eventName, _actorId, handler);
            _subscriptions.Add(subscription);
        }

        public void Dispose()
        {
            if (_subscriptions != null && _pubsub!=null)
            {
                foreach(var s in _subscriptions)
                {
                    try
                    {
                        _pubsub.Cancel(s).ConfigureAwait(false);
                    }
                    catch
                    {

                    }
                }
            }

            _subscriptions.Clear();
            _subscriptions = null;
        }
    }
}
