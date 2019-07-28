using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace NanoActor
{
    public interface IActor:IDisposable
    {
        String Id { get; set; }

        Task<object> Post(ITransportSerializer serializer,ActorRequest message, TimeSpan? timeout = null, CancellationToken? ct = null);

        event EventHandler DeactivateRequested;
    }


    public class WorkerActor : Attribute { }
    public class AllowParallel : Attribute { }

    public class AlwaysOn : Attribute { }
    
}
