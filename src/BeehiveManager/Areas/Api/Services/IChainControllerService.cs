using Etherna.BeehiveManager.Areas.Api.DtoModels;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public interface IChainControllerService
    {
        ChainStateDto? GetChainState();
    }
}