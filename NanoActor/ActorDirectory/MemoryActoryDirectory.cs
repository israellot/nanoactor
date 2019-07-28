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

        
        protected String GetStringKey(string actorTypeName, string actorId)
        {
            return $"{actorTypeName}:{actorId}";
        }

        public async ValueTask<StageAddressQueryResponse> GetAddress(string actorTypeName, string actorId)
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


      
        public  Task UnregisterActor(string actorTypeName,string actorId,string stageId)
        {
            var key = GetStringKey(actorTypeName,actorId);

            if (_hashset.TryGetValue(key,out var currentStageId))
            {
                if(stageId== currentStageId)
                    _hashset.TryRemove(key, out _);
            }

            return Task.CompletedTask;
        }

        

        public Task RegisterActor(string actorTypeName, string actorId,string stageId)
        {
            _hashset.AddOrUpdate(GetStringKey(actorTypeName, actorId), stageId, (a, b) => stageId);

            return Task.CompletedTask;
        }

        public Task<string> Reallocate(string actorTypeName, string actorId, string oldStageId)
        {
            throw new NotImplementedException();
        }

        public Task Refresh(string actorTypeName, string actorId)
        {
            throw new NotImplementedException();
        }

        public Task RemoveStage(string stageId)
        {
            throw new NotImplementedException();
        }
    }
}
