using Etherna.BeehiveManager.Domain.Models;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Serialization;

namespace Etherna.BeehiveManager.Persistence.ModelMaps
{
    class BeeNodeMap : IModelMapsCollector
    {
        public void Register(IDbContext dbContext)
        {
            // Register class maps.
            dbContext.SchemaRegister.AddModelMapsSchema<BeeNode>("6b94df32-034f-46f9-a5c1-239905ad5d07");
        }
    }
}
