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

    public delegate void SubscriptionHandler(string s, ref byte[] bytes);

    public interface IPubSub
    {        
        Task Subscribe(string channel, SubscriptionHandler handler);
        Task Unsubscribe(string channel, SubscriptionHandler handler);
        Task Publish(string channel,byte[] data);
        
    }
}
