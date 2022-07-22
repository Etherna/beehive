using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Services.Utilities;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class ChainControllerService : IChainControllerService
    {
        // Fields.
        private readonly IBeeNodeLiveManager liveManager;

        // Constructor.
        public ChainControllerService(
            IBeeNodeLiveManager liveManager)
        {
            this.liveManager = liveManager;
        }

        // Methods.
        public ChainStateDto? GetChainState() =>
            liveManager.ChainState is null ? null :
            new ChainStateDto(liveManager.ChainState);
    }
}
