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

using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Persistence.Repositories;
using Etherna.DomainEvents;
using Etherna.MongoDB.Driver.GridFS;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Repositories;
using PostageStamp = Etherna.Beehive.Domain.Models.PostageStamp;

namespace Etherna.Beehive.Persistence
{
    public interface IBeehiveDbContext : IDbContext
    {
        // Properties.
        //repositories
        IRepository<BeeNode, string> BeeNodes { get; }
        IRepository<ChunkPin, string> ChunkPins { get; }
        IRepository<PushingChunkRef, string> ChunkPushQueue { get; }

        /// <summary>
        /// The deprecated chunks of 0.4.0, read until migrated
        /// </summary>
        IRepository<LegacyChunk, string> Chunks { get; }

        /// <summary>
        /// The deprecated GridFS bucket of the chunks stored before 0.4.0, read until migrated
        /// </summary>
        IGridFSBucket ChunksBucket { get; }
        IRepository<ChunksMigration, string> ChunksMigrations { get; }
        IRepository<NodePinImport, string> NodePinImports { get; }
        IRepository<NodePinsImport, string> NodePinsImports { get; }
        IRepository<PostageBatchCache, string> PostageBatchesCache { get; }
        IRepository<PostageStamp, string> PostageStamps { get; }

        /// <summary>
        /// The chunks keyed by hash: a creation of a chunk held already adds its pins to the held one
        /// </summary>
        ISwarmChunksRepository SwarmChunks { get; }

        //others
        IEventDispatcher EventDispatcher { get; }
    }
}
