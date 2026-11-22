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
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Bson;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Utilities
{
    /// <summary>
    /// The deprecated stores of the chunks, as their migration reads and empties them: the chunks collection of
    /// 0.4.0, and the GridFS bucket before it
    /// </summary>
    public interface IDeprecatedChunkStores
    {
        /// <summary>
        /// Delete the documents of migrated chunks, each one only while it still carries the pins it was read with:
        /// a document that took or lost a pin meanwhile is left, to be migrated again
        /// </summary>
        /// <returns>The ids of the documents left because their pins changed</returns>
        Task<IReadOnlyCollection<string>> DeleteLegacyChunksAsync(
            IReadOnlyCollection<LegacyChunk> chunks,
            CancellationToken cancellationToken);

        Task DeleteGridFsFileAsync(ObjectId id, CancellationToken cancellationToken);

        Task<byte[]> DownloadGridFsFileAsync(ObjectId id, CancellationToken cancellationToken);

        Task<long> EstimateGridFsFilesAsync(CancellationToken cancellationToken);

        Task<long> EstimateLegacyChunksAsync(CancellationToken cancellationToken);

        /// <summary>
        /// A page of files of the GridFS bucket, in id order
        /// </summary>
        /// <param name="afterId">The id the page starts after: null for the first page</param>
        Task<IReadOnlyList<GridFsChunkFile>> ReadGridFsFilesAsync(
            ObjectId? afterId,
            int take,
            CancellationToken cancellationToken);

        /// <summary>
        /// A page of documents of the chunks collection, in id order: a document that can't be read is returned with
        /// its failure, instead of failing the page
        /// </summary>
        /// <param name="afterId">The id the page starts after: null for the first page</param>
        Task<IReadOnlyList<LegacyChunkDocument>> ReadLegacyChunksAsync(
            string? afterId,
            int take,
            CancellationToken cancellationToken);

        /// <summary>
        /// The documents of the chunks collection with the given ids, the ones still there
        /// </summary>
        Task<IReadOnlyList<LegacyChunkDocument>> ReadLegacyChunksAsync(
            IReadOnlyCollection<string> ids,
            CancellationToken cancellationToken);
    }
}
