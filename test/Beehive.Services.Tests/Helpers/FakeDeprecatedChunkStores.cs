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
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Bson;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Helpers
{
    /// <summary>
    /// The deprecated stores of the chunks in memory, with their semantics: pages in id order, a document read as a
    /// fresh instance each time, a delete conditioned on the pins the document was read with
    /// </summary>
    internal sealed class FakeDeprecatedChunkStores : IDeprecatedChunkStores
    {
        // Classes.
        public sealed class StoredLegacyChunk(string id, SwarmHash hash, byte[] payload, bool isSoc, DateTime creationDateTime)
        {
            public DateTime CreationDateTime { get; } = creationDateTime;
            public SwarmHash Hash { get; } = hash;
            public string Id { get; } = id;
            public bool IsSoc { get; } = isSoc;
            public byte[] Payload { get; } = payload;
            public List<ChunkPin> Pins { get; } = [];

            /// <summary>
            /// Why the document can't be read: null when it can
            /// </summary>
            public string? ReadFailure { get; init; }
        }

        public sealed class StoredGridFsFile(ObjectId id, string fileName, byte[] content, DateTime uploadDateTime)
        {
            public byte[] Content { get; } = content;
            public string FileName { get; } = fileName;
            public ObjectId Id { get; } = id;
            public DateTime UploadDateTime { get; } = uploadDateTime;
        }

        // Properties.
        public List<StoredGridFsFile> GridFsFiles { get; } = [];
        public List<StoredLegacyChunk> LegacyChunks { get; } = [];

        /// <summary>
        /// Invoked before a delete of documents applies, as a concurrent writer changing them meanwhile
        /// </summary>
        public Action<IReadOnlyCollection<LegacyChunk>>? OnDeletingLegacyChunks { get; set; }

        /// <summary>
        /// Invoked after a page of documents is read
        /// </summary>
        public Action? OnLegacyChunksPageRead { get; set; }

        // Methods.
        public Task<IReadOnlyCollection<string>> DeleteLegacyChunksAsync(
            IReadOnlyCollection<LegacyChunk> chunks,
            CancellationToken cancellationToken)
        {
            OnDeletingLegacyChunks?.Invoke(chunks);

            List<string> changedIds = [];
            foreach (var chunk in chunks)
            {
                var stored = LegacyChunks.SingleOrDefault(c => c.Id == chunk.Id);
                if (stored is null)
                    continue;
                if (stored.Pins.Select(p => p.Id).ToHashSet().SetEquals(chunk.Pins.Select(p => p.Id)))
                    LegacyChunks.Remove(stored);
                else
                    changedIds.Add(stored.Id);
            }
            return Task.FromResult<IReadOnlyCollection<string>>(changedIds);
        }

        public Task DeleteGridFsFileAsync(ObjectId id, CancellationToken cancellationToken)
        {
            GridFsFiles.RemoveAll(f => f.Id == id);
            return Task.CompletedTask;
        }

        public Task<byte[]> DownloadGridFsFileAsync(ObjectId id, CancellationToken cancellationToken) =>
            Task.FromResult(GridFsFiles.Single(f => f.Id == id).Content);

        public Task<long> EstimateGridFsFilesAsync(CancellationToken cancellationToken) =>
            Task.FromResult((long)GridFsFiles.Count);

        public Task<long> EstimateLegacyChunksAsync(CancellationToken cancellationToken) =>
            Task.FromResult((long)LegacyChunks.Count);

        public Task<IReadOnlyList<GridFsChunkFile>> ReadGridFsFilesAsync(
            ObjectId? afterId,
            int take,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<GridFsChunkFile>>(GridFsFiles
                .Where(f => afterId is null || f.Id > afterId.Value)
                .OrderBy(f => f.Id)
                .Take(take)
                .Select(f => new GridFsChunkFile(f.Id, f.FileName, f.UploadDateTime))
                .ToArray());

        public Task<IReadOnlyList<LegacyChunkDocument>> ReadLegacyChunksAsync(
            string? afterId,
            int take,
            CancellationToken cancellationToken)
        {
            var page = LegacyChunks
                .Where(c => afterId is null || string.CompareOrdinal(c.Id, afterId) > 0)
                .OrderBy(c => c.Id, StringComparer.Ordinal)
                .Take(take)
                .Select(ToDocument)
                .ToArray();
            OnLegacyChunksPageRead?.Invoke();
            return Task.FromResult<IReadOnlyList<LegacyChunkDocument>>(page);
        }

        public Task<IReadOnlyList<LegacyChunkDocument>> ReadLegacyChunksAsync(
            IReadOnlyCollection<string> ids,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<LegacyChunkDocument>>(LegacyChunks
                .Where(c => ids.Contains(c.Id))
                .OrderBy(c => c.Id, StringComparer.Ordinal)
                .Select(ToDocument)
                .ToArray());

        // Helpers.
        private static LegacyChunkDocument ToDocument(StoredLegacyChunk stored)
        {
            if (stored.ReadFailure is not null)
                return new LegacyChunkDocument(stored.Id, stored.ReadFailure);

            var chunk = new LegacyChunk(stored.Hash, stored.Payload, stored.IsSoc);
            ReflectionHelper.SetValue(chunk, c => c.Id, stored.Id);
            ReflectionHelper.SetValue(chunk, c => c.CreationDateTime, stored.CreationDateTime);
            foreach (var pin in stored.Pins)
                chunk.AddPin(pin);
            return new LegacyChunkDocument(chunk);
        }
    }
}
