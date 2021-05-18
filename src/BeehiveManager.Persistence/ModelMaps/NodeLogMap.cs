using Etherna.BeehiveManager.Domain.Models;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Serialization;

namespace Etherna.BeehiveManager.Persistence.ModelMaps
{
    class NodeLogMap : IModelMapsCollector
    {
        public void Register(IDbContext dbContext)
        {
            dbContext.SchemaRegister.AddModelMapsSchema<NodeLogBase>("a1c4d182-b3b2-4d49-8fe1-91d723c1188c");
            dbContext.SchemaRegister.AddModelMapsSchema<CashoutNodeLog>("625838e6-4290-42e4-a6d5-a6b0e5cf1660");
        }
    }
}
