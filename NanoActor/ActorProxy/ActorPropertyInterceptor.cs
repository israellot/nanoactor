using Castle.DynamicProxy;
using System;
using System.Collections.Generic;
using System.Text;

namespace NanoActor.ActorProxy
{
    public class ActorPropertyInterceptor : IInterceptor
    {
        Dictionary<string, object> internalDictionary = new Dictionary<string, object>();

        public void Intercept(IInvocation invocation)
        {

            if (invocation.Method.IsSpecialName)
            {
                if (invocation.Method.Name.StartsWith("set_"))
                {
                    var prop = invocation.Method.Name.Split(new[] { "set_" }, new StringSplitOptions() { })[1];

                    internalDictionary[prop] = invocation.Arguments[0];

                    return;
                }

                if (invocation.Method.Name.StartsWith("get_"))
                {
                    var prop = invocation.Method.Name.Split(new[] { "get_" }, new StringSplitOptions() { })[1];

                    var value = internalDictionary[prop];

                    invocation.ReturnValue = value;

                    return;
                }

            }


            invocation.Proceed();
        }
    }
}
