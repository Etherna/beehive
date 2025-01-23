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
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Stores;
using Etherna.MongODM.Core.Utility;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Tools
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    public sealed class DbChunkStore(
        IBeehiveDbContext dbContext)
        : ChunkStoreBase, IDbChunkStore
    {
        protected override async Task<SwarmChunk> LoadChunkAsync(SwarmHash hash)
        {
            using var dbExecContextHandler = new DbExecutionContextHandler(dbContext);

            var chunk = await dbContext.Chunks.TryFindOneAsync(c => c.Hash == hash);
            byte[]? payload = null;
            if (chunk is not null)
                payload = chunk.Payload.ToArray();
            payload ??= await dbContext.ChunksBucket.DownloadAsBytesByNameAsync(hash.ToString());
            
            return SwarmChunk.BuildFromSpanAndData(hash, payload);
        }

        protected override async Task<bool> DeleteChunkAsync(SwarmHash hash)
        {
            using var dbExecContextHandler = new DbExecutionContextHandler(dbContext);
            
            var chunk = await dbContext.Chunks.TryFindOneAsync(c => c.Hash == hash);
            if (chunk is null)
                return false;

            await dbContext.Chunks.DeleteAsync(chunk);
            return true;
        }

        protected override async Task<bool> SaveChunkAsync(SwarmChunk chunk)
        {
            ArgumentNullException.ThrowIfNull(chunk, nameof(chunk));
            
            using var dbExecContextHandler = new DbExecutionContextHandler(dbContext);
            
            try
            {
                var domainChunk = new Chunk(chunk.Hash, chunk.GetSpanAndData());
                await dbContext.Chunks.CreateAsync(domainChunk);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}