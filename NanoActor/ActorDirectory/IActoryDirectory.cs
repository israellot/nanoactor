using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.Directory
{
    public interface IActorDirectory
    {
        Task<StageAddressQueryResponse> GetAddress<ActorType>(string actorId);

        Task<StageAddressQueryResponse> GetAddress(Type actorType, string actorId);

        Task<StageAddressQueryResponse> GetAddress(string actorTypeName, string actorId);

        Task RegisterActor<ActorType>(string actorId,string stageId);

        Task RegisterActor(Type actorType, string actorId, string stageId);

        Task UnregisterActor<ActorType>(string actorId, string stageId);

    }
}
