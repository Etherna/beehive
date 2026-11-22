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
using Etherna.Beehive.Persistence;
using Etherna.Beehive.Services.Extensions;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.GridFS;
using Etherna.MongoDB.Driver.Linq;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using Microsoft.Extensions.Logging;
using System.Runtime.ExceptionServices;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Utilities
{
    /// <summary>
    /// The chunk store over beehive's db, falling back on the bee nodes for the chunks it doesn't hold: one node
    /// per chunk among the healthy ones, or the given source node only. A node's not found is final: the node
    /// already searched the network. What a node serves is saved in the db at once, with
    /// <paramref name="onSavingChunk"/> applied.
    /// The db holds one document per chunk, keyed by its hash: a chunk held already is not created again, it takes
    /// the pins of the saved one. Chunks are inserted optimistically, the db read only after a refused insert. The deprecated stores (the chunks of 0.4.0 and the GridFS bucket before them) are
    /// read after it, never written but for the pins of the chunks they hold.
    /// A chunk the db refuses fails the operation writing it: the write exception propagates, the chunks stay in
    /// the saving buffer, readable, and the store doesn't write again: every further add or flush throws the same
    /// failure at once, without a db round trip, and its dispose leaves the buffer alone
    /// </summary>
    [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
    public sealed class BeehiveChunkStore(
        IBeeNodeLiveManager beeNodeLiveManager,
        IBeehiveDbContext dbContext,
        ILogger logger,
        ISerializerModifierAccessor serializerModifierAccessor,
        int savingBufferLength = BeehiveChunkStore.DefaultSavingBufferLength,
        Action<Chunk>? onSavingChunk = null,
        BeeNodeLiveInstance? sourceNode = null)
        : ReadOnlyChunkStoreBase, IAsyncDisposable, IChunkStore, IDisposable
    {
        // Consts.
        public const int DefaultSavingBufferLength = 26000; //~100MB of data + intermediate chunks
        
        // Fields.
        private readonly ConcurrentDictionary<SwarmHash, Chunk> chunkSavingBuffer = new();
        private readonly SemaphoreSlim flushSemaphore = new(1, 1);
        
        private bool disposed;
        private ExceptionDispatchInfo? writeFailure;
        
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
        
            // Force a flush, unless a write already failed: the owner's exception stands.
            if (writeFailure is null)
            {
                var flushTask = FlushSaveAsync();
                flushTask.Wait();
            }

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

            // Force a flush, unless a write already failed: the owner's exception stands.
            if (writeFailure is null)
                await FlushSaveAsync();
            
            // Dispose managed resources.
            flushSemaphore.Dispose();
        
            disposed = true;
        }
        
        // Methods.
        public async Task<bool> AddAsync(SwarmChunk chunk, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(chunk);
            
            // A failure propagates to the writer: a false would read as "already present" in the sdk contract.
            // Once a write failed, the store throws it again at once: the sdk pipeline observes a failed stage only
            // at the end of the stream, and every remaining chunk would otherwise retry the failed write.
            writeFailure?.Throw();

            var domainChunk = new Chunk(chunk.Hash, chunk.GetFullPayload(), chunk is SwarmSoc);

            onSavingChunk?.Invoke(domainChunk);

            if (savingBufferLength > 0)
            {
                chunkSavingBuffer.TryAdd(chunk.Hash, domainChunk);
                if (chunkSavingBuffer.Count >= savingBufferLength)
                    await FlushSaveAsync();
            }
            else
            {
                try
                {
                    await SaveChunksAsync([domainChunk], cancellationToken);
                }
                catch (Exception e)
                {
                    writeFailure = ExceptionDispatchInfo.Capture(e);
                    throw;
                }
            }

            return true;
        }
        
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
                // A store whose write failed doesn't write again.
                writeFailure?.Throw();

                // Don't create on db with no chunks.
                var chunks = chunkSavingBuffer.Values.ToArray();
                if (chunks.Length == 0)
                    return;
                
                // Write on db before to remove from buffer. This permits to have chunks available always.
                // A failed write keeps them in the buffer and marks the store: no further write, not even on dispose.
                try
                {
                    await SaveChunksAsync(chunks, CancellationToken.None);
                }
                catch (Exception e)
                {
                    writeFailure = ExceptionDispatchInfo.Capture(e);
                    throw;
                }
                foreach (var chunk in chunks)
                    chunkSavingBuffer.TryRemove(chunk.Id, out _);
            }
            finally
            {
                flushSemaphore.Release();
            }
        }

        public override async Task<SwarmChunk> GetAsync(SwarmHash hash, CancellationToken cancellationToken = default)
        {
            // Try to find on buffer.
            if (chunkSavingBuffer.TryGetValue(hash, out var chunk))
                return ToSwarmChunk(hash, chunk.Payload, chunk.IsSoc);
            
            // Try to load from db.
            using(var _ = serializerModifierAccessor.EnableCacheSerializerModifier(true))
            using(var __ = new DbExecutionContextHandler(dbContext))
            {
                //try to find on repository
                var chunkModel = await dbContext.SwarmChunks.TryFindOneAsync(hash, cancellationToken);
                if (chunkModel is not null)
                    return ToSwarmChunk(hash, chunkModel.Payload, chunkModel.IsSoc);
                
                //fallback on the deprecated chunks of 0.4.0
                var legacyChunkModel = await dbContext.Chunks.TryFindOneAsync(c => c.Hash == hash, cancellationToken);
                if (legacyChunkModel is not null)
                    return ToSwarmChunk(hash, legacyChunkModel.Payload, legacyChunkModel.IsSoc);
            
                //fallback on old gridfs
                try
                {
                    var spanData = await dbContext.ChunksBucket.DownloadAsBytesByNameAsync(hash.ToString(), cancellationToken: cancellationToken);
                    return new SwarmCac(hash, spanData);
                }
                catch (GridFSFileNotFoundException)
                { }
            }
            
            // If it's not found, search on a healthy bee node.
            return await GetFromBeeNodeAsync(hash, cancellationToken);
        }

        public override async Task<IReadOnlyDictionary<SwarmHash, SwarmChunk?>> GetAsync(
            IEnumerable<SwarmHash> hashes,
            int? canReturnAfterFailed = null,
            int? canReturnAfterSucceeded = null,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(hashes);
            
            var missingHashes = new HashSet<SwarmHash>();
            var results = new Dictionary<SwarmHash, SwarmChunk?>();
            
            // Try to find on buffer.
            foreach (var hash in hashes)
            {
                if (chunkSavingBuffer.TryGetValue(hash, out var chunk))
                    results.Add(hash, ToSwarmChunk(hash, chunk.Payload, chunk.IsSoc));
                else
                    missingHashes.Add(hash);
            }
            if (missingHashes.Count == 0)
                return results;
            
            // Try to load from db.
            using(var _ = serializerModifierAccessor.EnableCacheSerializerModifier(true))
            using(var __ = new DbExecutionContextHandler(dbContext))
            {
                //try to find on repository
                var chunkModels = await dbContext.SwarmChunks.QueryElementsAsync(chunks =>
                    chunks.Where(c => missingHashes.Contains(c.Id))
                        .ToListAsync(cancellationToken));

                foreach (var chunkModel in chunkModels)
                {
                    results.TryAdd(chunkModel.Id, ToSwarmChunk(chunkModel.Id, chunkModel.Payload, chunkModel.IsSoc));
                    missingHashes.Remove(chunkModel.Id);
                }
                
                //fallback on the deprecated chunks of 0.4.0
                if (missingHashes.Count > 0)
                {
                    var legacyChunkModels = await dbContext.Chunks.QueryElementsAsync(chunks =>
                        chunks.Where(c => missingHashes.Contains(c.Hash))
                            .ToListAsync(cancellationToken));
                    foreach (var legacyChunkModel in legacyChunkModels)
                    {
                        results.TryAdd(legacyChunkModel.Hash,
                            ToSwarmChunk(legacyChunkModel.Hash, legacyChunkModel.Payload, legacyChunkModel.IsSoc));
                        missingHashes.Remove(legacyChunkModel.Hash);
                    }
                }
                
                //fallback on old gridfs
                foreach (var hash in missingHashes)
                {
                    try
                    {
                        var spanData = await dbContext.ChunksBucket.DownloadAsBytesByNameAsync(hash.ToString(),
                            cancellationToken: cancellationToken);
                        
                        results.TryAdd(hash, new SwarmCac(hash, spanData));
                        missingHashes.Remove(hash);
                    }
                    catch (GridFSFileNotFoundException) { }
                }
            }
            
            // If it's not found, search on healthy bee nodes.
            var fromBeeNodes = await GetFromBeeNodesAsync(missingHashes, cancellationToken);
            foreach (var (hash, chunk) in fromBeeNodes)
                results.TryAdd(hash, chunk);
            
            return results;
        }

        public override async Task<bool> HasChunkAsync(
            SwarmHash hash,
            CancellationToken cancellationToken = default)
        {
            // Try to find on buffer.
            if (chunkSavingBuffer.ContainsKey(hash))
                return true;
            
            // Try to find on db.
            using(var _ = serializerModifierAccessor.EnableCacheSerializerModifier(true))
            using(var __ = new DbExecutionContextHandler(dbContext))
            {
                //try to find on repository
                var chunkModel = await dbContext.SwarmChunks.TryFindOneAsync(hash, cancellationToken);
                if (chunkModel is not null)
                    return true;
                
                //fallback on the deprecated chunks of 0.4.0
                var legacyChunkModel = await dbContext.Chunks.TryFindOneAsync(c => c.Hash == hash, cancellationToken);
                if (legacyChunkModel is not null)
                    return true;
            
                //fallback on old gridfs
                try
                {
                    await dbContext.ChunksBucket.DownloadAsBytesByNameAsync(hash.ToString(), cancellationToken: cancellationToken);
                    return true;
                }
                catch (GridFSFileNotFoundException)
                { }
            }
            
            // If it's not found, search on the source node, or on a healthy bee node.
            var node = sourceNode ?? beeNodeLiveManager.SelectNearestHealthyNode(hash);
            return await node.ChunkStore.HasChunkAsync(hash, cancellationToken);
        }

        public async Task<bool> RemoveAsync(SwarmHash hash, CancellationToken cancellationToken = default)
        {
            using var dbExecContextHandler = new DbExecutionContextHandler(dbContext);

            // Try to remove from buffer.
            var found = chunkSavingBuffer.TryRemove(hash, out _);
            
            // Try to remove from repository.
            var chunk = await dbContext.SwarmChunks.TryFindOneAsync(hash, cancellationToken);
            if (chunk is not null)
            {
                found = true;
                await dbContext.SwarmChunks.DeleteAsync(chunk, cancellationToken);
            }
            
            // Try to remove from the deprecated chunks of 0.4.0.
            var legacyChunk = await dbContext.Chunks.TryFindOneAsync(c => c.Hash == hash, cancellationToken);
            if (legacyChunk is not null)
            {
                found = true;
                await dbContext.Chunks.DeleteAsync(legacyChunk, cancellationToken);
            }
            
            // Try to remove from old gridfs.
            try
            {
                await using var downStream = await dbContext.ChunksBucket.OpenDownloadStreamByNameAsync(
                    hash.ToString(),
                    cancellationToken: cancellationToken);
                found = true;
                var id = downStream.FileInfo.Id;
                await dbContext.ChunksBucket.DeleteAsync(id, cancellationToken);
            }
            catch { }

            return found;
        }
        
        // Helpers.
        private async Task AddPinsToHeldChunkAsync(Chunk heldChunk, CancellationToken cancellationToken)
        {
            var hash = heldChunk.Id;
            foreach (var pin in heldChunk.Pins)
                await dbContext.SwarmChunks.TryFindOneAndAddToSetAsync(
                    new ExpressionFilterDefinition<Chunk>(c => c.Id == hash),
                    c => c.Pins,
                    pin,
                    new FindOneAndUpdateOptions<Chunk>(),
                    cancellationToken);
        }

        private static bool IsDuplicateKeyFailure(Exception exception) =>
            exception switch
            {
                MongoWriteException e => e.WriteError.Category == ServerErrorCategory.DuplicateKey,
                MongoBulkWriteException e => e.WriteErrors.Any(w => w.Category == ServerErrorCategory.DuplicateKey),
                { InnerException: not null } => IsDuplicateKeyFailure(exception.InnerException),
                _ => false
            };

        private static SwarmChunk ToSwarmChunk(SwarmHash hash, ReadOnlyMemory<byte> payload, bool isSoc) =>
            isSoc ?
                SwarmSoc.BuildFromBytes(hash, payload, new SwarmChunkBmt()) :
                new SwarmCac(hash, payload);

        private async Task<SwarmChunk> GetFromBeeNodeAsync(SwarmHash hash, CancellationToken cancellationToken)
        {
            // Get from the source node, or from the nearest healthy one.
            var node = sourceNode ?? beeNodeLiveManager.SelectNearestHealthyNode(hash);
            var chunk = await node.ChunkStore.GetAsync(hash, cancellationToken: cancellationToken);
            logger.ChunkReadFromNode(hash, node.Id);

            await SaveFetchedChunksAsync([chunk], cancellationToken);
            return chunk;
        }

        private async Task<IReadOnlyDictionary<SwarmHash, SwarmChunk>> GetFromBeeNodesAsync(
            HashSet<SwarmHash> hashes,
            CancellationToken cancellationToken)
        {
            /*
             * Algorithm:
             * Scope is try to balance requests to nodes as much as possible.
             * We need an efficient way to select healthy nodes for each hash, keeping distance between hashes and
             * selected nodes as minimal as possible. Nodes are not equally distributed, and because of this we can't
             * simply select the nearest node, this would create systematic unbalances on loads.
             * Instead, hashes are not equally distributed too, but any unbalance would be random and not systematic.
             *
             * With these considerations, we will retrieve the list of healthy nodes, order them by overlay addresses,
             * and will create equal address partitions to assign hashes to each node. The hash will be assigned to
             * a single partition, and each partition will be searched with its node.
             *
             * In this way load balancing is guaranteed with a high number of hashes, and nodes tend to receive nearest
             * hashes to search. Everything has linear complexity on the number of hashes, that is optimal.
             *
             * Partitioning is made reading first two bytes, and casting to uint16. The hash.ToBucketId() is a ready
             * and perfect implementation of this.
             *
             * Each hash is asked to its node only. A node looks for a chunk in its local store and then retrieves it
             * from the network, so its not found is final: another node of ours would repeat the same retrieval,
             * with its timeouts, for a chunk the network doesn't have.
             */

            if (hashes.Count == 0)
                return new Dictionary<SwarmHash, SwarmChunk>();

            // With a source node, everything is read from it.
            if (sourceNode is not null)
            {
                var foundOnSourceNode = await ReadFromNodeAsync(sourceNode, hashes, cancellationToken);
                await SaveFetchedChunksAsync(foundOnSourceNode.Values, cancellationToken);
                return foundOnSourceNode;
            }
            
            var healthyNodes = beeNodeLiveManager.HealthyNodes
                .Where(n => n.Status.Addresses != null)
                .OrderBy(n => n.Status.Addresses!.Overlay).ToArray();

            if (healthyNodes.Length == 0)
                return new Dictionary<SwarmHash, SwarmChunk>();

            // Each node is asked the partition of the hashes nearest to it.
            var hashesByNode = new Dictionary<BeeNodeLiveInstance, List<SwarmHash>>();
            foreach (var hash in hashes)
            {
                var hashBucketId = hash.ToBucketId();
                var nodeIndex = (int)((hashBucketId * (uint)healthyNodes.Length) >> 16);
                hashesByNode.TryAdd(healthyNodes[nodeIndex], []);
                hashesByNode[healthyNodes[nodeIndex]].Add(hash);
            }

            var reads = hashesByNode
                .Select(p => ReadFromNodeAsync(p.Key, p.Value, cancellationToken))
                .ToArray();
            await Task.WhenAll(reads); //a node failing its read fails the fetch: the failure propagates

            var found = new Dictionary<SwarmHash, SwarmChunk>();
            foreach (var read in reads)
                foreach (var (hash, chunk) in await read) //already completed
                    found[hash] = chunk;

            await SaveFetchedChunksAsync(found.Values, cancellationToken);
            return found;
        }

        /// <summary>
        /// Read a batch of chunks from a node: the chunks it serves. The ones it doesn't hold are logged at Debug
        /// and left out, a failure of the read propagates
        /// </summary>
        private async Task<IReadOnlyDictionary<SwarmHash, SwarmChunk>> ReadFromNodeAsync(
            BeeNodeLiveInstance node,
            IReadOnlyCollection<SwarmHash> hashes,
            CancellationToken cancellationToken)
        {
            var result = await node.ChunkStore.GetAsync(hashes, cancellationToken: cancellationToken);
            var found = result
                .Where(p => p.Value != null)
                .ToDictionary(p => p.Key, p => p.Value!);
            logger.ChunksReadFromNode(found.Count, hashes.Count, node.Id);
            if (found.Count < hashes.Count)
                logger.ChunksNotFoundOnNode(node.Id, hashes.Where(h => !found.ContainsKey(h)));
            return found;
        }

        /// <summary>
        /// Save chunks in the db, one document per hash, inserting them optimistically: the db is read only when the
        /// insert is refused for a hash it holds already, by now or because a concurrent writer created it. Then the
        /// held ones take the pins of the saved ones, and the others are saved again in two halves, each inserted
        /// optimistically in turn: every refusal leaves one more hash held, and the halves shrink down to a single
        /// chunk, which a refusal proves held
        /// </summary>
        private async Task SaveChunksAsync(Chunk[] chunks, CancellationToken cancellationToken)
        {
            try
            {
                await dbContext.SwarmChunks.CreateAsync(chunks, cancellationToken);
                return;
            }
            catch (Exception e) when (IsDuplicateKeyFailure(e))
            {
                //a hash is held: by now, or created meanwhile by a concurrent writer
            }

            // A single chunk refused is held.
            if (chunks.Length == 1)
            {
                await AddPinsToHeldChunkAsync(chunks[0], cancellationToken);
                return;
            }

            // Read which chunks are held, and save the others again in halves.
            var hashes = chunks.Select(c => c.Id).ToArray();
            var heldHashes = (await dbContext.SwarmChunks.QueryElementsAsync(elements =>
                elements.Where(c => hashes.Contains(c.Id))
                    .Select(c => c.Id)
                    .ToListAsync(cancellationToken))).ToHashSet();
            foreach (var heldChunk in chunks.Where(c => heldHashes.Contains(c.Id)))
                await AddPinsToHeldChunkAsync(heldChunk, cancellationToken);

            var notHeldChunks = chunks.Where(c => !heldHashes.Contains(c.Id)).ToArray();
            if (notHeldChunks.Length == 0)
                return;
            var halfLength = (notHeldChunks.Length + 1) / 2;
            await SaveChunksAsync(notHeldChunks[..halfLength], cancellationToken);
            if (notHeldChunks.Length > halfLength)
                await SaveChunksAsync(notHeldChunks[halfLength..], cancellationToken);
        }

        /// <summary>
        /// Save in the local db the chunks read from the nodes, flushing them at once: a pinning reading through
        /// the store attaches its pin to the documents right after, so they must be on the db already
        /// </summary>
        private async Task SaveFetchedChunksAsync(IEnumerable<SwarmChunk> chunks, CancellationToken cancellationToken)
        {
            foreach (var chunk in chunks)
                await AddAsync(chunk, cancellationToken);
            await FlushSaveAsync();
        }
    }
}
