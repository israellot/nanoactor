using MessagePack;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor
{

    [MessagePackObject]
    public class ActorRequest
    {

        [Key(0)]
        public Guid Id { get; set; }

        [Key(1)]
        public Boolean FireAndForget { get; set; }

        [Key(2)]
        public virtual String ActorInterface { get; set; }

        [Key(3)]
        public String ActorId { get; set; }

        [Key(4)]
        public virtual String ActorMethodName { get; set; }

        [Key(5)]
        public List<byte[]> Arguments { get; set; }

        [Key(6)]
        public Boolean FromClient { get; set; }

        [Key(7)]
        public Boolean WorkerActor { get; set; }

        public ActorRequest() {
            this.Id = Guid.NewGuid();
            this.ActorId = String.Empty;
        }


    }

    
    public class LocalActorRequest:ActorRequest
    {
        
        public MethodInfo ActorMethod { get; set; }

        public object[] ArgumentObjects { get; set; }

        public override string ActorInterface {
            get{
                return $"{this.ActorMethod.DeclaringType.Namespace}.{this.ActorMethod.DeclaringType.Name}";
            }
            set
            {

            }
        }

        public override string ActorMethodName
        {
            get
            {
                return this.ActorMethod.Name;
            }
            set
            {

            }
        }

        public LocalActorRequest():base()
        {
            
        }

        public ActorRequest ToRemote(ITransportSerializer _serializer)
        {
            var request = new ActorRequest();

            List<byte[]> arguments = new List<byte[]>();
            foreach (var argument in this.ArgumentObjects)
            {
                arguments.Add(_serializer.Serialize(argument));
            }
            request.Arguments = arguments;
            request.ActorInterface = this.ActorInterface;
            request.ActorMethodName = this.ActorMethod.Name;

            request.ActorId = this.ActorId;
            request.FireAndForget = this.FireAndForget;
            request.FromClient = this.FromClient;
            request.Id = this.Id;
            request.WorkerActor = this.WorkerActor;

            

            return request;
        }

    }
}
