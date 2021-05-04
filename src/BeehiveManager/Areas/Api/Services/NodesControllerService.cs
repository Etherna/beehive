using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Domain;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class NodesControllerService : INodesControllerService
    {
        // Fields.
        private readonly IBeehiveContext context;

        // Constructor.
        public NodesControllerService(
            IBeehiveContext context)
        {
            this.context = context;
        }

        public Task<IEnumerable<BeeNodeDto>> GetBeeNodesAsync(int page, int take)
        {
            throw new System.NotImplementedException();
        }
    }
}
