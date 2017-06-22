using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace NanoActor
{
    public class LocalActorMessageQueue : IActorMessageQueue
    {
        BufferBlock<ActorRequest> requestQueue;

        ConcurrentDictionary<Guid, WriteOnceBlock<object>> responseQueues;

        public LocalActorMessageQueue()
        {
            requestQueue = new BufferBlock<ActorRequest>(new DataflowBlockOptions()
            {
                EnsureOrdered = true                
            });

            responseQueues = new ConcurrentDictionary<Guid, WriteOnceBlock<object>>();
        }

        public void Enqueue(ActorRequest message)
        {
            requestQueue.Post(message);
        }

        public void EnqueueResponse(ActorRequest message,object response)
        {
            if(responseQueues.TryGetValue(message.Id,out var buffer)){
                buffer.Post(response);
                buffer.Complete();
            }
        }

        public async Task<object> EnqueueAndWaitResponse(ActorRequest message,TimeSpan? timeout=null,CancellationToken? ct=null)
        {
            var responseBuffer = responseQueues.GetOrAdd(message.Id, new WriteOnceBlock<object>(null));

            Enqueue(message);

            timeout = timeout ?? TimeSpan.FromMilliseconds(-1);

            var response =  await responseBuffer.ReceiveAsync(timeout.Value, ct??CancellationToken.None);

            

            responseQueues.TryRemove(message.Id, out _);

            return response;
        }

        public async Task<ActorRequest> Dequeue(CancellationToken ct)
        {
            while (true)
            {
                var receive = await requestQueue.ReceiveAsync(TimeSpan.FromMinutes(1), ct);

                if (receive != null)
                {
                    return receive;
                }

                if (ct.IsCancellationRequested)
                    return null;
            }
        }
    }
}
