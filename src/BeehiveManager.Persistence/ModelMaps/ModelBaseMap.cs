using Etherna.BeehiveManager.Domain.Models;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Serializers;

namespace Etherna.BeehiveManager.Persistence.ModelMaps
{
    class ModelBaseMap : IModelMapsCollector
    {
        public void Register(IDbContext dbContext)
        {
            // register class maps.
            dbContext.SchemaRegister.AddModelMapsSchema<ModelBase>("7653dfab-f715-42d1-8d3d-bbca69755399");

            dbContext.SchemaRegister.AddModelMapsSchema<EntityModelBase<string>>("3d7b0f5d-d490-495e-af05-6114e8f8d2f4", modelMap =>
            {
                modelMap.AutoMap();

                // Set Id representation.
                modelMap.IdMemberMap.SetSerializer(new StringSerializer(BsonType.ObjectId))
                                    .SetIdGenerator(new StringObjectIdGenerator());
            });
        }
    }
}
