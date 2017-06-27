using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.Directory
{
    public class MemoryStageAddress
    {
        public Type ActorType { get; set; }

        public String ActorId { get; set; }
    }

    public class MemoryActorDirectory : IActorDirectory
    {

        public ConcurrentDictionary<string, string> _hashset;

        public MemoryActorDirectory()
        {
            _hashset = new ConcurrentDictionary<string, string>();
        }

        protected String GetStringKey<ActorType>(string actorId)
        {
            return GetStringKey(typeof(ActorType), actorId);
        }
        protected String GetStringKey(Type actorType,string actorId)
        {
            return GetStringKey(actorType.Name, actorId);
        }
        protected String GetStringKey(string actorTypeName, string actorId)
        {
            return $"{actorTypeName}:{actorId}";
        }

        public async Task<StageAddressQueryResponse> GetAddress(string actorTypeName, string actorId)
        {
            if (_hashset.TryGetValue(GetStringKey(actorTypeName, actorId), out var stageId))
            {
                return new StageAddressQueryResponse() {  Found=true,StageId=stageId };
            }
            else
            {
                return new StageAddressQueryResponse() { Found = false };
            }
        }

        public Task<StageAddressQueryResponse> GetAddress(Type actorType, string actorId)
        {
            return GetAddress(actorType.Name, actorId);
        }

        public Task<StageAddressQueryResponse> GetAddress<ActorType>(string actorId)
        {
            return GetAddress(typeof(ActorType), actorId);
        }

      
        public  Task UnregisterActor<ActorType>(string actorId,string stageId)
        {
            var key = GetStringKey<ActorType>(actorId);

            if (_hashset.TryGetValue(key,out var currentStageId))
            {
                if(stageId== currentStageId)
                    _hashset.TryRemove(key, out _);
            }

            return Task.CompletedTask;
        }

        public Task RegisterActor<ActorType>(string actorId,string stageId)
        {

            return RegisterActor(typeof(ActorType), actorId, stageId);
        }

        public Task RegisterActor(Type actorType, string actorId,string stageId)
        {
            _hashset.AddOrUpdate(GetStringKey(actorType, actorId), stageId, (a, b) => stageId);

            return Task.CompletedTask;
        }

       
    }
}
