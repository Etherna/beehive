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

using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Services
{
    public interface IChunksControllerService
    {
        /// <summary>
        /// Handle bulk chunks upload
        /// </summary>
        /// <param name="batchId">Postage batch Id</param>
        /// <param name="payload">Bulk upload payload</param>
        /// <returns>Status code</returns>
        Task<int> ChunksBulkUploadAsync(
            PostageBatchId batchId,
            byte[] payload);

        /// <summary>
        /// Download a single chunk
        /// </summary>
        /// <param name="hash">The chunk's hash</param>
        /// <returns>Request result</returns>
        Task<IResult> DownloadChunkAsync(SwarmHash hash);
    }
}