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
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Bson;
using Etherna.MongoDB.Bson.IO;
using Etherna.MongoDB.Bson.Serialization;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.GridFS;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Utilities
{
    public class DeprecatedChunkStores(IBeehiveDbContext dbContext) : IDeprecatedChunkStores
    {
        // Consts.
        private const string GridFsFilesCollectionName = "chunks.files";

        // Methods.
        public Task<IReadOnlyCollection<string>> DeleteLegacyChunksAsync(
            IReadOnlyCollection<LegacyChunk> chunks,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(chunks);
            if (chunks.Count == 0)
                return Task.FromResult<IReadOnlyCollection<string>>([]);

            return dbContext.Chunks.AccessToCollectionAsync<IReadOnlyCollection<string>>(async collection =>
            {
                // One delete per document, each one conditioned on the pins it was read with, in one round trip: the
                // pins are only added and removed, never replaced, so the same count holding the same ids is the same set.
                var filterBuilder = Builders<LegacyChunk>.Filter;
                var result = await collection.BulkWriteAsync(
                    chunks.Select(c =>
                    {
                        var idFilter = filterBuilder.Eq("_id", ObjectId.Parse(c.Id));
                        var pinIds = c.Pins.Select(p => new BsonObjectId(ObjectId.Parse(p.Id))).ToArray();
                        return new DeleteOneModel<LegacyChunk>(pinIds.Length == 0 ?
                            idFilter & (filterBuilder.Size("Pins", 0) | filterBuilder.Exists("Pins", false)) :
                            idFilter & filterBuilder.Size("Pins", pinIds.Length) & filterBuilder.All("Pins._id", pinIds));
                    }),
                    new BulkWriteOptions { IsOrdered = false },
                    cancellationToken);
                if (result.DeletedCount == chunks.Count)
                    return [];

                // The documents still there changed their pins meanwhile.
                var ids = chunks.Select(c => ObjectId.Parse(c.Id)).ToArray();
                var leftIds = await collection.Find(Builders<LegacyChunk>.Filter.In("_id", ids))
                    .Project(Builders<LegacyChunk>.Projection.Include("_id"))
                    .ToListAsync(cancellationToken);
                return leftIds.Select(d => d["_id"].AsObjectId.ToString()).ToArray();
            });
        }

        public Task DeleteGridFsFileAsync(ObjectId id, CancellationToken cancellationToken) =>
            dbContext.ChunksBucket.DeleteAsync(id, cancellationToken);

        public Task<byte[]> DownloadGridFsFileAsync(ObjectId id, CancellationToken cancellationToken) =>
            dbContext.ChunksBucket.DownloadAsBytesAsync(id, cancellationToken: cancellationToken);

        public Task<long> EstimateGridFsFilesAsync(CancellationToken cancellationToken) =>
            dbContext.ChunksBucket.Database.GetCollection<BsonDocument>(GridFsFilesCollectionName)
                .EstimatedDocumentCountAsync(cancellationToken: cancellationToken);

        public Task<long> EstimateLegacyChunksAsync(CancellationToken cancellationToken) =>
            dbContext.Chunks.EstimatedDocumentCountAsync(cancellationToken);

        public async Task<IReadOnlyList<GridFsChunkFile>> ReadGridFsFilesAsync(
            ObjectId? afterId,
            int take,
            CancellationToken cancellationToken)
        {
            var filter = afterId is null ?
                Builders<GridFSFileInfo>.Filter.Empty :
                Builders<GridFSFileInfo>.Filter.Gt(f => f.Id, afterId.Value);
            using var cursor = await dbContext.ChunksBucket.FindAsync(
                filter,
                new GridFSFindOptions
                {
                    Limit = take,
                    Sort = Builders<GridFSFileInfo>.Sort.Ascending(f => f.Id)
                },
                cancellationToken);
            var files = await cursor.ToListAsync(cancellationToken);

            return files.Select(f => new GridFsChunkFile(f.Id, f.Filename, f.UploadDateTime)).ToArray();
        }

        public Task<IReadOnlyList<LegacyChunkDocument>> ReadLegacyChunksAsync(
            string? afterId,
            int take,
            CancellationToken cancellationToken) =>
            ReadLegacyChunksAsync(
                afterId is null ?
                    Builders<LegacyChunk>.Filter.Empty :
                    Builders<LegacyChunk>.Filter.Gt("_id", ObjectId.Parse(afterId)),
                take,
                cancellationToken);

        public Task<IReadOnlyList<LegacyChunkDocument>> ReadLegacyChunksAsync(
            IReadOnlyCollection<string> ids,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(ids);

            return ReadLegacyChunksAsync(
                Builders<LegacyChunk>.Filter.In("_id", ids.Select(ObjectId.Parse)),
                ids.Count,
                cancellationToken);
        }

        // Helpers.
        /// <summary>
        /// Read the documents one by one from their raw content: a document that can't be read fails alone. The
        /// chunks read leave the db context at once: a document read again deserializes its current content, not the
        /// instance loaded by the previous read, and a scan of the whole collection doesn't keep it in memory
        /// </summary>
        [SuppressMessage("Design", "CA1031:Do not catch general exception types", Justification = "A document failing to read must not fail the others")]
        private Task<IReadOnlyList<LegacyChunkDocument>> ReadLegacyChunksAsync(
            FilterDefinition<LegacyChunk> filter,
            int take,
            CancellationToken cancellationToken) =>
            dbContext.Chunks.AccessToCollectionAsync<IReadOnlyList<LegacyChunkDocument>>(async collection =>
            {
                var documents = await collection.Find(filter)
                    .Sort(Builders<LegacyChunk>.Sort.Ascending("_id"))
                    .Limit(take)
                    .As<BsonDocument>()
                    .ToListAsync(cancellationToken);

                List<LegacyChunkDocument> chunkDocuments = [];
                using var transientModelsScope = dbContext.StartTransientModelsScope();
                foreach (var document in documents)
                {
                    var id = document["_id"].ToString()!;
                    try
                    {
                        using var documentReader = new BsonDocumentReader(document);
                        var chunk = collection.DocumentSerializer.Deserialize(
                            BsonDeserializationContext.CreateRoot(documentReader));
                        chunkDocuments.Add(new LegacyChunkDocument(chunk));
                    }
                    catch (Exception e)
                    {
                        chunkDocuments.Add(new LegacyChunkDocument(id, $"{e.GetType().Name}: {e.Message}"));
                    }
                }
                return chunkDocuments;
            });
    }
}
