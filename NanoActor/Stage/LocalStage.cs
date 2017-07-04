using Microsoft.Extensions.DependencyInjection;
using NanoActor.Directory;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using System.Linq;
using NanoActor.PubSub;
using Microsoft.Extensions.Logging;
using NanoActor.Telemetry;

namespace NanoActor
{

   public class LocalStageMetrics
    {



    }

    public class LocalActorInstance
    {
        public Actor Instance { get; set; }

        public String ActorId { get; set; }

        public String ActorTypeName { get; set; }

        public DateTimeOffset ActivationTimestamp { get; set; }

        public DateTimeOffset LastAccess { get; set; }

    }

    public class LocalStage
    {

        Dictionary<string, Type> _actorMap = new Dictionary<string, Type>();

        ConcurrentDictionary<string, LocalActorInstance> _actorInstances = new ConcurrentDictionary<string, LocalActorInstance>();

        IServiceProvider _services;

        IActorDirectory _actorDirectory;

        IStageDirectory _stageDirectory;

        ILogger _logger;

        ITelemetry _telemetry;

        ITransportSerializer _serializer;

        MeterTracker _reqSecondMeter;

        PubSubManager _pubsub;

        public String StageGuid { get; private set; }

        public Boolean Enabled { get; set; } = false;

        public LocalStage(IServiceProvider services,ITransportSerializer serializer, ITelemetry<LocalStage> telemetry, IActorDirectory actorDirectory, IStageDirectory stageDirectory,ILogger<LocalStage> logger, PubSubManager pubsub) {
            _services = services;

            _actorDirectory = actorDirectory;

            _stageDirectory = stageDirectory;

            _pubsub = pubsub;

            _serializer = serializer;

            StageGuid = Guid.NewGuid().ToString();

            _logger = logger;

            _telemetry = telemetry;

            _reqSecondMeter = _telemetry.Meter("Stage.Requests", TimeSpan.FromSeconds(10));
        }

        public void Run()
        {
            

            this.Enabled = true;

            LocalMonitorTask();
        }

        public void DeactivateInstance(string actorTypeName,string actorId)
        {

        }

        public void LocalMonitorTask()
        {
            Task.Run(async () => {

                while (true)
                {
                    try
                    {
                        await Task.Delay(TimeSpan.FromMinutes(5));

                        var now = DateTimeOffset.Now;

                        _actorInstances.Keys.AsParallel()
                        .ForAll(key => {
                            if (_actorInstances.TryGetValue(key, out var instance))
                            {
                                if(now- instance.LastAccess > TimeSpan.FromHours(1))
                                {
                                    _actorInstances.TryRemove(key, out _);
                                     instance.Instance.Dispose();
                                    _logger.LogDebug("Deactivated instance for {0}. Actor Id : {1}. No activity", instance.ActorTypeName, instance.ActorId);
                                }
                            }
                        });

                        //foreach (var key in _actorInstances.Keys)
                        //{

                        //    if (_actorInstances.TryGetValue(key, out var instance))
                        //    {
                        //        var actorAddress = await _actorDirectory.GetAddress(instance.ActorTypeName, instance.ActorId);

                        //        if (actorAddress.StageId != StageGuid)
                        //        {
                        //            //not on this stage anymore
                        //            _actorInstances.TryRemove(key, out _);
                        //            instance.Instance.Dispose();
                        //            _logger.LogDebug("Deactivated instance for {0}. Actor Id : {1}", instance.ActorTypeName, instance.ActorId);
                        //        }

                        //    }

                        //}

                        


                    }
                    catch (Exception ex)
                    {

                    }


                }




            }).ConfigureAwait(false);
        }

        public Boolean HasInstanceFor<ActorType>(string actorId)
        {
            return HasInstanceFor(typeof(ActorType), actorId);
        }

        public Boolean HasInstanceFor(Type actorType, string actorId)
        {            
            return _actorInstances.ContainsKey(string.Join(",", actorType, actorId));
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
                    if ($"{ti.Namespace}.{ti.Name}" == interfaceName)
                    {
                        return ti.AsType();
                    }
                }

            }

            return null;
        }

        ConcurrentDictionary<string, AsyncLock> _activationLocks = new ConcurrentDictionary<string, AsyncLock>();

        public async Task<LocalActorInstance> ActivateInstance(string actorTypeName,String actorId)
        {
            


            var actorInstanceKey = string.Join(",", actorTypeName, actorId);

            if (_actorInstances.TryGetValue(actorInstanceKey, out var actorInstance))
            {
                return actorInstance;
            }
            else
            {
                var asyncLock = _activationLocks.GetOrAdd(actorInstanceKey, new AsyncLock());

                using (await asyncLock.LockAsync())
                {
                    if (!_actorInstances.TryGetValue(actorInstanceKey, out actorInstance))
                    {
                        if (!_actorMap.TryGetValue(actorTypeName, out var actorType))
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

                        //mark instance
                        await _actorDirectory.RegisterActor(actorTypeName, actorId, StageGuid);

                        //try a registered service
                        Object actor = null;
                        try
                        {
                            actor = _services.GetRequiredService(actorType);
                        }
                        catch (Exception ex)
                        {

                        }
                        

                        actorInstance = new LocalActorInstance()
                        {
                            ActorId = actorId,
                            ActivationTimestamp = DateTimeOffset.UtcNow,
                            LastAccess = DateTimeOffset.UtcNow,
                            Instance = (Actor)actor,
                            ActorTypeName=actorTypeName
                        };

                        if (_actorInstances.TryAdd(actorInstanceKey, actorInstance))
                        {
                            actorInstance.Instance.Id = actorId;
                            actorInstance.Instance.Configure(actorTypeName, _pubsub);
                            await actorInstance.Instance.Run();
                        }

                        _logger.LogDebug("Activated instance for {0}. Actor Id : {1}", actorTypeName, actorId);
                    }

                    
                }
                _activationLocks.TryRemove(string.Join(":", actorTypeName, actorId), out _);


                return actorInstance;
            }

        }

        public async Task<ActorResponse> Execute(ActorRequest message,TimeSpan? timeout=null)
        {
            _reqSecondMeter.Tick();

            var actorInstance = await ActivateInstance(message.ActorInterface, message.ActorId);

            actorInstance.LastAccess = DateTimeOffset.UtcNow;
            try
            {
                var result = await actorInstance.Instance.Post(_serializer, message, timeout);

                if (result is Exception)
                {
                    var response = new ActorResponse()
                    {
                        Success = false,
                        Exception = (Exception)result,
                        Id = message.Id
                    };

                    return response;
                }
                else
                {
                    var response = new ActorResponse()
                    {
                        Success = true,
                        Response = _serializer.Serialize(result),
                        Id = message.Id
                    };

                    return response;
                }
            }
            catch(Exception ex)
            {
                var response = new ActorResponse()
                {
                    Success = false,
                    Exception = (Exception)ex,
                    Id = message.Id
                };

                return response;
            }
                               
        }

        public Task WaitInstanceIdle(string actorTypeName, String actorId)
        {
            var actorInstanceKey = string.Join(",", actorTypeName, actorId);

            if (_actorInstances.TryGetValue(actorInstanceKey, out var actorInstance))
            {
                return actorInstance.Instance.WaitIdle();
            }

            return Task.CompletedTask;
        }

    }
}
