using Etherna.BeehiveManager.Domain;
using Etherna.MongoDB.Driver;
using Moq;
using System;

namespace Etherna.BeehiveManager.Persistence.Helpers
{
    public class DeserializationTestElement<TModel>
    {
        public DeserializationTestElement(string sourceDocument, TModel expectedModel) :
            this(sourceDocument, expectedModel, (_, _) => { })
        { }

        public DeserializationTestElement(
            string sourceDocument,
            TModel expectedModel,
            Action<Mock<IMongoDatabase>, IBeehiveDbContext> setupAction)
        {
            SourceDocument = sourceDocument;
            ExpectedModel = expectedModel;
            SetupAction = setupAction;
        }

        public string SourceDocument { get; }
        public TModel ExpectedModel { get; }
        public Action<Mock<IMongoDatabase>, IBeehiveDbContext> SetupAction { get; }
    }
}
