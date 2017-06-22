using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.Directory
{
    public interface IActorDirectory
    {
        Task<StageAddress> GetAddress<ActorType>(string actorId);

        Task<StageAddress> GetAddress(Type actorType, string actorId);

        Task<StageAddress> GetAddress(string actorTypeName, string actorId);

        Task RegisterActor<ActorType>(string actorId);

        Task RegisterActor(Type actorType, string actorId);

        Task UnregisterActor<ActorType>(string actorId);

    }
}
