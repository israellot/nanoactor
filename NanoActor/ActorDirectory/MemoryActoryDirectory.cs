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

        public ConcurrentDictionary<string, object> _hashset;

        public MemoryActorDirectory()
        {
            _hashset = new ConcurrentDictionary<string, object>();
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

        public async Task<StageAddress> GetAddress(string actorTypeName, string actorId)
        {
            if (_hashset.TryGetValue(GetStringKey(actorTypeName, actorId), out _))
            {
                return new StageAddress() { IsLocal = true };
            }
            else
            {
                return new StageAddress() { NotFound = true };
            }
        }

        public Task<StageAddress> GetAddress(Type actorType, string actorId)
        {
            return GetAddress(actorType.Name, actorId);
        }

        public Task<StageAddress> GetAddress<ActorType>(string actorId)
        {
            return GetAddress(typeof(ActorType), actorId);
        }

      
        public async Task UnregisterActor<ActorType>(string actorId)
        {
            _hashset.TryRemove(GetStringKey<ActorType>(actorId), out _);
        }

        public Task RegisterActor<ActorType>(string actorId)
        {

            return RegisterActor(typeof(ActorType), actorId);
        }

        public async Task RegisterActor(Type actorType, string actorId)
        {
            _hashset.AddOrUpdate(GetStringKey(actorType, actorId), actorId, (a, b) => { return b; });
        }

       
    }
}
