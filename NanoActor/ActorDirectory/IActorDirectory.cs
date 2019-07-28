using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.Directory
{
    public interface IActorDirectory
    {
             
        ValueTask<StageAddressQueryResponse> GetAddress(string actorTypeName, string actorId);

        Task RegisterActor(string actorTypeName, string actorId, string stageId);

        Task UnregisterActor(string actorTypeName,string actorId, string stageId);

        Task<string> Reallocate(string actorTypeName, string actorId, string oldStageId);

        Task Refresh(string actorTypeName, string actorId);

        Task RemoveStage(string stageId);
    }
}
