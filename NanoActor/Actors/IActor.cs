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

        Task<object> Post(ActorRequest message, TimeSpan? timeout = null, CancellationToken? ct = null);

        
    }

    

    
}
