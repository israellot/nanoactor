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
using Microsoft.Extensions.Options;
using NanoActor.Options;

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

        MetricTracker _actorInstancesMetric;

        MeterTracker _deactivatedInstancesMeter;

        PubSubManager _pubsub;

        NanoServiceOptions _serviceOptions;

        public String StageGuid { get; private set; }

        public Boolean Enabled { get; set; } = false;

        public LocalStage(
            IServiceProvider services,
            ITransportSerializer serializer,
            ITelemetry<LocalStage> telemetry,
            IActorDirectory actorDirectory,
            IStageDirectory stageDirectory,
            ILogger<LocalStage> logger,
            IOptions<NanoServiceOptions> serviceOptions,
            PubSubManager pubsub) {
            _services = services;

            _actorDirectory = actorDirectory;

            _stageDirectory = stageDirectory;

            _pubsub = pubsub;

            _serializer = serializer;

            StageGuid = Guid.NewGuid().ToString();

            _logger = logger;

            _telemetry = telemetry;
            _serviceOptions = serviceOptions.Value;

            var stageDefaultProperty = new Dictionary<string, string>
                    {
                        { "StageId", StageGuid }
                    };

            _telemetry.SetProperties(stageDefaultProperty);


            _reqSecondMeter = _telemetry.Meter(
                "Stage.Requests",TimeSpan.FromSeconds(60)
                );

            _actorInstancesMetric = _telemetry.Metric(
                "Stage.ActiveActorInstances");

            _deactivatedInstancesMeter = _telemetry.Meter("Stage.DeactivatedActorInstances", TimeSpan.FromSeconds(60));
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
                        await Task.Delay(TimeSpan.FromSeconds(30));

                        var now = DateTimeOffset.Now;

                        _actorInstances.Keys.AsParallel()
                        .WithDegreeOfParallelism(4)
                        .ForAll(key => {
                            if (_actorInstances.TryGetValue(key, out var instance))
                            {
#if !RELEASE
                                if(now- instance.LastAccess > TimeSpan.FromMinutes(1))
#else
                                if(now- instance.LastAccess > TimeSpan.FromSeconds(_serviceOptions.DefaultActorTTL) )
#endif
                                {
                                    _actorInstances.TryRemove(key, out _);
                                    _logger.LogDebug("Deactivated instance for {0}. Actor Id : {1}. No activity", instance.ActorTypeName, instance.ActorId);
                                    instance.Instance.Dispose();
                                    instance = null;

                                    _deactivatedInstancesMeter.Tick();
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
                        _telemetry.Exception(ex);
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

            _actorInstancesMetric.Track(_actorInstances.Count);

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

                        actor = _services.GetRequiredService(actorType);
                        
                        actorInstance = new LocalActorInstance()
                        {
                            ActorId = actorId,
                            ActivationTimestamp = DateTimeOffset.UtcNow,
                            LastAccess = DateTimeOffset.UtcNow,
                            Instance = (Actor)actor,
                            ActorTypeName=actorTypeName
                        };

                        try
                        {
                            actorInstance.Instance.Id = actorId;
                            actorInstance.Instance.Configure(actorTypeName, _pubsub);
                            await actorInstance.Instance.Run();
                            if(!_actorInstances.TryAdd(actorInstanceKey, actorInstance))
                            {
                                actorInstance.Instance.Dispose();
                            }
                        }
                        catch (Exception ex)
                        {
                            throw new Exception("Failed to initialize actor", ex);
                        }

                        

                        _logger.LogDebug("Activated instance for {0}. Actor Id : {1}", actorTypeName, actorId);
                    }

                    
                }
                _activationLocks.TryRemove(actorInstanceKey, out _);


                return actorInstance;
            }

        }

        public async Task<ActorResponse> Execute(ActorRequest message,TimeSpan? timeout=null)
        {
            _reqSecondMeter.Tick();

            var actorInstanceKey = string.Join(",", message.ActorInterface, message.ActorId);

            var actorInstance = await ActivateInstance(message.ActorInterface, message.ActorId);

            actorInstance.LastAccess = DateTimeOffset.UtcNow;


            var track = _serviceOptions.TrackActorExecutionDependencyCalls ?
                _telemetry.Dependency($"actor:{message.ActorInterface}", message.ActorMethodName) : null;

            try
            {
              
                var result = await actorInstance.Instance.Post(_serializer, message, timeout);

                track?.End(true);

                var response = new ActorResponse()
                {
                    Success = true,
                    Response = _serializer.Serialize(result),
                    Id = message.Id
                };

                return response;
            }
            catch(Exception ex)
            {
                
                track?.End(false);

                var response = new ActorResponse()
                {
                    Success = false,
                    //Exception = (Exception)ex,
                    Id = message.Id
                };                

                _actorInstances.TryRemove(actorInstanceKey, out var instance);
                instance.Instance.Dispose();

                _telemetry.Exception(ex,"ActorException");

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

        public async Task Stop()
        {
            var instances = _actorInstances.Values.ToList();

            var tasks = new List<Task>();

            foreach(var i in instances)
            {
                var waitTask = Task.Run(async () => {
                    try
                    {
                        await i.Instance.WaitIdle();
                        i.Instance.Dispose();
                    }
                    catch { }
                });

                tasks.Add(waitTask);
            }

            await Task.WhenAll(tasks.ToArray());



        }

    }
}
