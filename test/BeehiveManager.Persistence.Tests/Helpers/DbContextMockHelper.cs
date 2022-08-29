using Etherna.ExecContext.AsyncLocal;
using Etherna.MongoDB.Bson.Serialization;
using Etherna.MongoDB.Driver;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Conventions;
using Etherna.MongODM.Core.Domain.Models;
using Etherna.MongODM.Core.Options;
using Etherna.MongODM.Core.ProxyModels;
using Etherna.MongODM.Core.Repositories;
using Etherna.MongODM.Core.Serialization.Mapping;
using Etherna.MongODM.Core.Serialization.Modifiers;
using Etherna.MongODM.Core.Utility;
using Moq;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace Etherna.BeehiveManager.Persistence.Helpers
{
    public static class DbContextMockHelper
    {
        [SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "Need to keep objects after test construction")]
        public static void InitializeDbContextMock(DbContext dbContext, Mock<IMongoDatabase>? mongoDatabaseMock = null)
        {
            if (dbContext is null)
                throw new ArgumentNullException(nameof(dbContext));

            // Setup dbcontext dependencies for initialization.
            Mock<IDbDependencies> dbDependenciesMock = new();
            var execContext = AsyncLocalContext.Instance;

            dbDependenciesMock.Setup(d => d.BsonSerializerRegistry).Returns(new BsonSerializerRegistry());
            dbDependenciesMock.Setup(d => d.DbCache).Returns(new DbCache());
            dbDependenciesMock.Setup(d => d.DbMaintainer).Returns(new Mock<IDbMaintainer>().Object);
            dbDependenciesMock.Setup(d => d.DbMigrationManager).Returns(new Mock<IDbMigrationManager>().Object);
            dbDependenciesMock.Setup(d => d.DiscriminatorRegistry).Returns(new DiscriminatorRegistry());
            dbDependenciesMock.Setup(d => d.ExecutionContext).Returns(execContext);
            dbDependenciesMock.Setup(d => d.ProxyGenerator).Returns(new ProxyGenerator(new Castle.DynamicProxy.ProxyGenerator()));
            dbDependenciesMock.Setup(d => d.RepositoryRegistry).Returns(new RepositoryRegistry());
            dbDependenciesMock.Setup(d => d.SchemaRegistry).Returns(new SchemaRegistry());
            dbDependenciesMock.Setup(d => d.SerializerModifierAccessor).Returns(new SerializerModifierAccessor(execContext));

            // Setup Mongo client.
            mongoDatabaseMock ??= new Mock<IMongoDatabase>();

            var mongoClientMock = new Mock<IMongoClient>();
            mongoClientMock.Setup(c => c.GetDatabase(It.IsAny<string>(), It.IsAny<MongoDatabaseSettings>()))
                .Returns(mongoDatabaseMock.Object);

            // Register static integration with drivers.
            BsonSerializer.TryRegisterDiscriminatorConvention(typeof(object),
                new HierarchicalProxyTolerantDiscriminatorConvention("_t", execContext));

            BsonSerializer.SetSerializationContextAccessor(new SerializationContextAccessor(execContext));

            // Initialize dbContext.
            dbContext.Initialize(
                dbDependenciesMock.Object,
                mongoClientMock.Object,
                new DbContextOptions(),
                Array.Empty<IDbContext>());

            // Disable creation of proxy models, for test scope.
            dbContext.ProxyGenerator.DisableCreationWithProxyTypes = true;
        }

        public static Mock<IMongoCollection<TModel>> SetupCollectionMock<TModel, TKey>(
            Mock<IMongoDatabase> mongoDatabaseMock,
            ICollectionRepository<TModel, TKey> collection)
             where TModel : class, IEntityModel<TKey>
        {
            if (mongoDatabaseMock is null)
                throw new ArgumentNullException(nameof(mongoDatabaseMock));
            if (collection is null)
                throw new ArgumentNullException(nameof(collection));

            var collectionMock = new Mock<IMongoCollection<TModel>>();

            mongoDatabaseMock.Setup(d => d.GetCollection<TModel>(collection.Name, It.IsAny<MongoCollectionSettings>()))
                .Returns(() => collectionMock.Object);

            return collectionMock;
        }

        public static void SetupFindWithPredicate<TModel>(
            Mock<IMongoCollection<TModel>> collectionMock,
            Func<FilterDefinition<TModel>, IEnumerable<TModel>> modelSelector)
        {
            if (collectionMock is null)
                throw new ArgumentNullException(nameof(collectionMock));

            // Setup collection.
            collectionMock.Setup(c => c.FindAsync(
                It.IsAny<FilterDefinition<TModel>>(),
                It.IsAny<FindOptions<TModel, TModel>>(),
                It.IsAny<CancellationToken>()))
                .ReturnsAsync<
                    FilterDefinition<TModel>,
                    FindOptions<TModel, TModel>,
                    CancellationToken,
                    IMongoCollection<TModel>,
                    IAsyncCursor<TModel>>((filter, _, _) =>
                    {
                        // Setup cursor.
                        bool isFirstBatch = true;
                        var cursorMock = new Mock<IAsyncCursor<TModel>>();

                        cursorMock.Setup(c => c.MoveNextAsync(It.IsAny<CancellationToken>()))
                            .ReturnsAsync(() =>
                            {
                                var wasFirstbatch = isFirstBatch;
                                isFirstBatch = false;
                                return wasFirstbatch;
                            });
                        cursorMock.Setup(c => c.Current)
                            .Returns(modelSelector(filter));

                        return cursorMock.Object;
                    });
        }
    }
}
