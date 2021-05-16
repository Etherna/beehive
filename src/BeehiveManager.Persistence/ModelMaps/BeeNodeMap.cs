using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Domain.Models.BeeNodeAgg;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Serialization;

namespace Etherna.BeehiveManager.Persistence.ModelMaps
{
    class BeeNodeMap : IModelMapsCollector
    {
        public void Register(IDbContext dbContext)
        {
            dbContext.SchemaRegister.AddModelMapsSchema<BeeNode>("6b94df32-034f-46f9-a5c1-239905ad5d07");

            // Aggregate models.
            dbContext.SchemaRegister.AddModelMapsSchema<BeeNodeAddresses>("b4fc3145-6864-43d0-8ba5-c43f36877519");
            dbContext.SchemaRegister.AddModelMapsSchema<BeeNodeStatus>("e86940fb-0eee-4cea-bf01-187738325976");
        }
    }
}
