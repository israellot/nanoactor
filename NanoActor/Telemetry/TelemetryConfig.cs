using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace NanoActor
{
    public class TelemetryConfig:Attribute
    {

        public Boolean Supress { get; set; }

        public TelemetryConfig(Boolean supress=false)
        {
            this.Supress = supress;
        }

        public static TelemetryConfig GetConfig(MethodInfo method)
        {

            var methodAttribute = method.GetCustomAttribute<TelemetryConfig>(true);

            if (methodAttribute != null)
                return methodAttribute;

            var globalAttribute = method.DeclaringType.GetTypeInfo()
                    .GetCustomAttribute<TelemetryConfig>();

            if (globalAttribute != null)
                return globalAttribute;


            return new TelemetryConfig();

        }

        
    }
}
