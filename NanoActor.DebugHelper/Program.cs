using System;
using NanoActor;
using System.Threading.Tasks;

namespace NanoActor.DebugHelper
{
    public static class Program
    {

        public interface ITestActor : IActor
        {
            Task<string> Hello(string hello);
        }

        public class TestActor : Actor, ITestActor
        {
            public async Task<string> Hello(string hello)
            {
                return hello+" World";
            }
        }

        public static void Main()
        {

            var message = new ActorMessage()
            {
                ActorMethodName = "Hello",
                ActorInterface = "ITestActor",
                Arguments = new object[] { "Hello" },
                ActorId = null
            };

            var actor = new TestActor();

            actor.Post(message);

            while (true)
            {
                Console.ReadKey();
                
            }

        }
    }
}
