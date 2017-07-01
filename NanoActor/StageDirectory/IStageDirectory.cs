using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace NanoActor
{
    public interface IStageDirectory
    {

        Task<StageAddress> GetStageAddress(string stageId);

        Task<StageAddress> RegisterStage(string stageId, SocketAddress address);

        Task UnregisterStage(string stageId);

        Task<List<string>> GetAllStages();


    }
}
