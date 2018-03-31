using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor
{
    public interface IStageDirectory
    {
                
        Task<Boolean> RegisterStage(string stageId);

        Task<Boolean> IsLive(string stageId);

        Task UnregisterStage(string stageId);

        Task<List<string>> GetAllStages(Boolean forceUpdate=false);


    }
}
