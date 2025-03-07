// Copyright 2021-present Etherna SA
// This file is part of Beehive.
// 
// Beehive is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Beehive is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Beehive.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.Beehive.Domain;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Persistence.Repositories;
using Etherna.DomainEvents;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.GridFS;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Repositories;
using Etherna.MongODM.Core.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Persistence
{
    public class BeehiveDbContext(
        IEventDispatcher eventDispatcher,
        BeeNode[]? seedDbBeeNodes)
        : DbContext, IBeehiveDbContext, IEventDispatcherDbContext
    {
        // Consts.
        private const string ModelMapsNamespace = "Etherna.Beehive.Persistence.ModelMaps";

        // Fields.
        private GridFSBucket? _chunksBucket;

        // Properties.
        //repositories
        public IRepository<BeeNode, string> BeeNodes { get; } = new DomainRepository<BeeNode, string>(
            new RepositoryOptions<BeeNode>("beeNodes")
            {
                IndexBuilders =
                [
                    (Builders<BeeNode>.IndexKeys.Ascending(n => n.ConnectionString), new CreateIndexOptions<BeeNode> { Unique = true })
                ]
            });
        public IRepository<ChunkPinLock, string> ChunkPinLocks { get; } = new DomainRepository<ChunkPinLock, string>(
            new RepositoryOptions<ChunkPinLock>("chunkPinLocks")
            {
                IndexBuilders =
                [
                    (Builders<ChunkPinLock>.IndexKeys.Ascending(l => l.ResourceId),
                        new CreateIndexOptions<ChunkPinLock> { Unique = true })
                ]
            });
        public IRepository<ChunkPin, string> ChunkPins { get; } = new DomainRepository<ChunkPin, string>(
            new RepositoryOptions<ChunkPin>("chunkPins")
            {
                IndexBuilders =
                [
                    (Builders<ChunkPin>.IndexKeys.Ascending(p => p.Hash),
                        new CreateIndexOptions<ChunkPin> { Unique = true })
                ]
            });
        public IRepository<UploadedChunkRef, string> ChunkPushQueue { get; } =
            new Repository<UploadedChunkRef, string>("chunkPushQueue");
        public IRepository<Chunk, string> Chunks { get; } =
            new Repository<Chunk, string>(new RepositoryOptions<Chunk>("chunks")
            {
                IndexBuilders =
                [
                    (Builders<Chunk>.IndexKeys.Ascending(c => c.CreationDateTime), new CreateIndexOptions<Chunk>()),
                    (Builders<Chunk>.IndexKeys.Ascending(c => c.Hash), new CreateIndexOptions<Chunk>())
                ]
            });
        public GridFSBucket ChunksBucket
        {
            get
            {
                return _chunksBucket ??= new GridFSBucket(Database, new GridFSBucketOptions
                {
                    BucketName = "chunks",
                    WriteConcern = WriteConcern.WMajority,
                    ReadPreference = ReadPreference.Secondary
                });
            }
        }
        public IRepository<PostageBatchLock, string> PostageBatchLocks { get; } = new DomainRepository<PostageBatchLock, string>(
            new RepositoryOptions<PostageBatchLock>("postageBatchLocks")
            {
                IndexBuilders =
                [
                    (Builders<PostageBatchLock>.IndexKeys.Ascending(l => l.ResourceId),
                        new CreateIndexOptions<PostageBatchLock> { Unique = true })
                ]
            });
        public IRepository<PostageBucketsCache, string> PostageBucketsCache { get; } = new Repository<PostageBucketsCache, string>(
            new RepositoryOptions<PostageBucketsCache>("postageBucketsCache")
            {
                IndexBuilders =
                [
                    (Builders<PostageBucketsCache>.IndexKeys.Ascending(b => b.BatchId),
                        new CreateIndexOptions<PostageBucketsCache> { Unique = true })
                ]
            });

        //other properties
        public IEventDispatcher EventDispatcher { get; } = eventDispatcher;

        // Protected properties.
        protected override IEnumerable<IModelMapsCollector> ModelMapsCollectors =>
            from t in typeof(BeehiveDbContext).GetTypeInfo().Assembly.GetTypes()
            where t.IsClass && t.Namespace == ModelMapsNamespace
            where t.GetInterfaces().Contains(typeof(IModelMapsCollector))
            select Activator.CreateInstance(t) as IModelMapsCollector;

        // Public methods.
        public override async Task SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            var changedEntityModels = ChangedModelsList.OfType<EntityModelBase>().ToArray();

            // Save changes.
            await base.SaveChangesAsync(cancellationToken);
            
            // Dispatch events.
            foreach (var model in changedEntityModels)
            {
                await EventDispatcher.DispatchAsync(model.Events);
                model.ClearEvents();
            }
        }

        // Protected methods.
        protected override async Task SeedAsync()
        {
            if (seedDbBeeNodes is null)
                return;

            foreach (var node in seedDbBeeNodes)
                await BeeNodes.CreateAsync(node);
        }
    }
}
