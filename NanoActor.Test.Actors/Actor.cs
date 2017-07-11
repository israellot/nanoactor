using System;
using System.Threading.Tasks;
using NanoActor.ActorProxy;

namespace NanoActor.Test.Actors
{
    public interface ITestActor : IActor
    {
        Task<string> Hello(string hello);

        Task NoReturn(string hello);

        Task Throw();

        Task RaiseEvent();
    }

    public class TestEvent
    {
        public string Field1 { get; set; }

        public string Field2 { get; set; }
    }

    public class TestActor : Actor, ITestActor
    {
        public TestActor(IServiceProvider services) : base(services)
        {

        }

        public Task<string> Hello(string hello)
        {
            return Task.FromResult(hello + " World " + this.Id);
        }

        public async Task NoReturn(string hello)
        {
            await Task.Delay(50);
        }

        public async Task RaiseEvent()
        {
            await this.Publish("testEvent", new TestEvent() { Field1 = "1", Field2 = "2" });
        }

        public async Task Throw()
        {
            throw new Exception("");
        }

        protected override async Task OnAcvitate()
        {
            
        }

        protected override Task SaveState()
        {
            throw new NotImplementedException();
        }
    }
}
