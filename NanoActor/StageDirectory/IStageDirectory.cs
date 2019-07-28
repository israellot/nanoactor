using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor
{
    public interface IStageDirectory
    {
                
        Task<Boolean> RegisterStage(string stageId);

        ValueTask<Boolean> IsLive(string stageId);

        Task UnregisterStage(string stageId);

        ValueTask<List<string>> GetAllStages(Boolean forceUpdate=false);


    }
}
