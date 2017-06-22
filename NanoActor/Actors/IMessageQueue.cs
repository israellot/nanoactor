using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NanoActor
{
    public interface IActorMessageQueue
    {
        void Enqueue(ActorRequest message);

        Task<object> EnqueueAndWaitResponse(ActorRequest message, TimeSpan? timeout = null, CancellationToken? ct = null);

        void EnqueueResponse(ActorRequest message, object response);

        Task<ActorRequest> Dequeue(CancellationToken ct);
    }
}
