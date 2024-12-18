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

using Etherna.Beehive.Areas.Api.Services;
using Etherna.Beehive.Attributes;
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Controllers
{
    [ApiController]
    public class ChunksController(IChunksControllerService service)
        : ControllerBase
    {
        // Get.

        [HttpGet("/chunks/{*address:minlength(1)}")]
        [HttpGet("/v1/chunks/{*address:minlength(1)}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IResult> DownloadChunkAsync(SwarmHash hash) =>
            service.DownloadChunkAsync(hash);

        // Post.

        [HttpPost("/chunks/bulk-upload")] //obsolete route
        [HttpPost("/ev1/chunks/bulk-upload")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task ChunksBulkUploadAsync()
        {
            // Get headers.
            HttpContext.Request.Headers.TryGetValue(
                SwarmHttpConsts.SwarmPostageBatchId,
                out var batchIdHeaderValue);
            var batchId = PostageBatchId.FromString(batchIdHeaderValue.Single()!);
            
            // Read payload.
            await using var memoryStream = new MemoryStream();
            await HttpContext.Request.Body.CopyToAsync(memoryStream);
            var payload = memoryStream.ToArray();
            
            // Invoke service.
            var statusCode = await service.ChunksBulkUploadAsync(
                batchId,
                payload);
            HttpContext.Response.StatusCode = statusCode;
        }
    }
}