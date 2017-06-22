﻿using Microsoft.Extensions.DependencyInjection;
using NanoActor.Directory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;

namespace NanoActor
{

    public class LocalActorInstance
    {
        public object Instance { get; set; }

        public String ActorId { get; set; }

        public DateTimeOffset ActivationTimestamp { get; set; }

    }

    public class LocalStage
    {

        Dictionary<string, Type> _actorMap = new Dictionary<string, Type>();

        ConcurrentDictionary<Type, ConcurrentDictionary<string, LocalActorInstance>> _actorInstances = new ConcurrentDictionary<Type, ConcurrentDictionary<string, LocalActorInstance>>();

        IServiceProvider _services;

        IActorDirectory _actorDirectory;

        public Boolean Enabled { get; set; } = true;

        public LocalStage(IServiceProvider services, IActorDirectory actorDirectory) {
            _services = services;

            _actorDirectory = actorDirectory;
        }

        public Boolean HasInstanceFor<ActorType>(string actorId)
        {
            return HasInstanceFor(typeof(ActorType), actorId);
        }

        public Boolean HasInstanceFor(Type actorType, string actorId)
        {
            var instanceDictionary = _actorInstances.GetOrAdd(actorType, new ConcurrentDictionary<string, LocalActorInstance>());

            return instanceDictionary.ContainsKey(actorId);
        }

        public Boolean CanProcessMessage(ActorRequest message)
        {
            if (_actorMap.TryGetValue(message.ActorInterface, out var actorType))
            {
                return HasInstanceFor(actorType, message.ActorId);

            }

            return false;
        }

        public Type FindInterface(string interfaceName)
        {
            System.Reflection.Assembly ass = System.Reflection.Assembly.GetEntryAssembly();
                        
            foreach (System.Reflection.TypeInfo ti in ass.DefinedTypes)
            {
                if($"{ti.Namespace}.{ti.Name}" == interfaceName)
                {
                    return ti.AsType();
                }
            }

            var allAssemblies = ass.GetReferencedAssemblies();

            foreach(var assemblyName in allAssemblies)
            {
                var assembly = Assembly.Load(assemblyName);

                foreach (System.Reflection.TypeInfo ti in assembly.DefinedTypes)
                {
                    if ($"{ti.Namespace}:{ti.Name}" == interfaceName)
                    {
                        return ti.AsType();
                    }
                }

            }

            return null;
        }

        public async Task<LocalActorInstance> ActivateInstance(string actorTypeName,String actorId)
        {
            if(!_actorMap.TryGetValue(actorTypeName,out var actorType))
            {
                actorType = FindInterface(actorTypeName);

                if (actorType != null)
                {
                    _actorMap[actorTypeName] = actorType;
                }
                else
                {
                    throw new ArgumentException("Couldn't find actor service", "actorTypeName");
                }
            }


            var instanceDictionary = _actorInstances.GetOrAdd(actorType, new ConcurrentDictionary<string, LocalActorInstance>());

            if (instanceDictionary.TryGetValue(actorId, out var actorInstance))
            {
                return actorInstance;
            }
            else
            {
                //mark instance
                await _actorDirectory.RegisterActor(actorType, actorId);

                //try a registered service
                var actor = _services.GetRequiredService(actorType);

                actorInstance = new LocalActorInstance()
                {
                    ActorId = actorId,
                    ActivationTimestamp = DateTimeOffset.UtcNow,
                    Instance = actor
                };

                actorInstance = instanceDictionary.GetOrAdd(actorInstance.ActorId, actorInstance);

                ((Actor)actorInstance.Instance).Run();


                return actorInstance;
            }

        }

        public async Task<object> Execute(ActorRequest message)
        {
            var actorInstance = await ActivateInstance(message.ActorInterface, message.ActorId);

            return await ((Actor)actorInstance.Instance).Post(message,TimeSpan.FromSeconds(5));
        }

    }
}
