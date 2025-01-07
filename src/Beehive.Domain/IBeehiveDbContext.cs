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
using Etherna.DomainEvents;
using Etherna.MongoDB.Driver.GridFS;
using Etherna.MongODM.Core;
using Etherna.MongODM.Core.Repositories;

namespace Etherna.Beehive.Domain
{
    public interface IBeehiveDbContext : IDbContext
    {
        // Properties.
        //repositories
        IRepository<BeeNode, string> BeeNodes { get; }
        IRepository<ChunkPinLock, string> ChunkPinLocks { get; }
        IRepository<ChunkPin, string> ChunkPins { get; }
        IRepository<UploadedChunkRef, string> ChunkPushQueue { get; }
        IRepository<Chunk, string> Chunks { get; }
        GridFSBucket ChunksBucket { get; }

        //others
        IEventDispatcher EventDispatcher { get; }
    }
}
