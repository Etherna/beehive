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
using Etherna.MongoDB.Driver.GridFS;
using Etherna.MongODM.Core.Utility;
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Utilities
{
    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    public sealed class BeehiveChunkStore(
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        int savingBufferLength = BeehiveChunkStore.DefaultSavingBufferLength,
        Action<Chunk>? onSavingChunk = null)
        : ChunkStoreBase, IAsyncDisposable, IDisposable
    {
        // Consts.
        public const int DefaultSavingBufferLength = 10000; //~40MB
        
        // Fields.
        private readonly ConcurrentDictionary<SwarmHash, Chunk> chunkSavingBuffer = new();
        private readonly SemaphoreSlim flushSemaphore = new(1, 1);
        
        private bool disposed;
        
        // Destructor and dispose.
        ~BeehiveChunkStore() => Dispose(false);
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
        private void Dispose(bool disposing)
        {
            if (disposed) return;
        
            // Force a flush.
            var flushTask = FlushSaveAsync();
            flushTask.Wait();

            // Dispose managed resources.
            if (disposing)
                flushSemaphore.Dispose();
        
            disposed = true;
        }
        public async ValueTask DisposeAsync()
        {
            await DisposeAsyncCore();
            GC.SuppressFinalize(this);
        }
        private async ValueTask DisposeAsyncCore()
        {
            if (disposed) return;

            // Force a flush.
            await FlushSaveAsync();
            
            // Dispose managed resources.
            flushSemaphore.Dispose();
        
            disposed = true;
        }
        
        // Methods.
        public async Task FlushSaveAsync()
        {
            /*
             * Use flush semaphore to avoid concurrent flushing requests.
             * This could happen if a burst of chunks is wrote faster than the buffer to flush chunks.
             * Each time that the buffer size is larger than the max size, flush is invoked. But if another flush
             * is already operating, we don't want to split the flush work in different db requests.
             *
             * Wait and don't skip because exiting from a flush we need to be sure that all the already present chunks
             * have been written on db.
             */
            await flushSemaphore.WaitAsync();

            try
            {
                // Don't create on db with no chunks.
                var chunks = chunkSavingBuffer.Values.ToArray();
                if (chunks.Length == 0)
                    return;
                
                // Write on db before to remove from buffer. This permits to have chunks available always.
                await dbContext.Chunks.CreateAsync(chunks); 
                foreach (var chunk in chunks)
                    chunkSavingBuffer.TryRemove(chunk.Hash, out _);
            }
            finally
            {
                flushSemaphore.Release();
            }
        }
        
        // Protected methods.
        protected override async Task<bool> DeleteChunkAsync(SwarmHash hash)
        {
            using var dbExecContextHandler = new DbExecutionContextHandler(dbContext);

            // Try remove from buffer.
            var found = chunkSavingBuffer.TryRemove(hash, out _);
            
            // Try remove from repository.
            var chunk = await dbContext.Chunks.TryFindOneAsync(c => c.Hash == hash);
            if (chunk is not null)
            {
                found = true;
                await dbContext.Chunks.DeleteAsync(chunk);
            }
            
            // Try remove from old gridfs.
            try
            {
                using var downStream = await dbContext.ChunksBucket.OpenDownloadStreamByNameAsync(hash.ToString());
                found = true;
                var id = downStream.FileInfo.Id;
                await dbContext.ChunksBucket.DeleteAsync(id);
            }
            catch { }

            return found;
        }

        protected override async Task<SwarmChunk> LoadChunkAsync(SwarmHash hash, CancellationToken cancellationToken = default)
        {
            // Try to find on buffer.
            if (chunkSavingBuffer.TryGetValue(hash, out var chunk))
            {
                var payload = chunk.Payload.ToArray();
                return SwarmChunk.BuildFromSpanAndData(hash, payload);
            }
            
            // Try load from db.
            using(var _ = new DbExecutionContextHandler(dbContext))
            {
                //try to find on repository
                var chunkModel = await dbContext.Chunks.TryFindOneAsync(c => c.Hash == hash, cancellationToken);
                if (chunkModel is not null)
                {
                    var payload = chunkModel.Payload.ToArray();
                    return SwarmChunk.BuildFromSpanAndData(hash, payload);
                }
            
                //fallback on old gridfs
                try
                {
                    var payload = await dbContext.ChunksBucket.DownloadAsBytesByNameAsync(hash.ToString(), cancellationToken: cancellationToken);
                    return SwarmChunk.BuildFromSpanAndData(hash, payload);
                }
                catch (GridFSFileNotFoundException)
                { }
            }
            
            // If it's not found, search on a healthy bee node.
            var node = await beeNodeLiveManager.SelectHealthyNodeAsync();
            var beeClientChunkStore = new BeeClientChunkStore(node.Client);
            return await beeClientChunkStore.GetAsync(hash, cancellationToken: cancellationToken);
        }
        
        protected override async Task<bool> SaveChunkAsync(SwarmChunk chunk)
        {
            ArgumentNullException.ThrowIfNull(chunk, nameof(chunk));
            
            try
            {
                var domainChunk = new Chunk(chunk.Hash, chunk.GetSpanAndData());

                onSavingChunk?.Invoke(domainChunk);

                if (savingBufferLength > 0)
                {
                    chunkSavingBuffer.TryAdd(chunk.Hash, domainChunk);
                    if (chunkSavingBuffer.Count >= savingBufferLength)
                        await FlushSaveAsync();
                }
                else
                {
                    await dbContext.Chunks.CreateAsync(domainChunk);
                }
                
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}