using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor.PubSub
{
    public class PublishedMessage
    {
        string Channel { get; set; }
        byte[] Data { get; set; }
    }

    public interface IPubSub
    {
        Task Subscribe(string channel, Action<string,byte[]> handler);
        Task Unsubscribe(string channel, Action<string, byte[]> handler);
        Task Publish(string channel,byte[] data);
        
    }
}
