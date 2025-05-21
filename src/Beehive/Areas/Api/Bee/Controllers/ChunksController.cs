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

using Etherna.Beehive.Areas.Api.Bee.DtoModels;
using Etherna.Beehive.Areas.Api.Bee.Services;
using Etherna.Beehive.Attributes;
using Etherna.Beehive.Configs;
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Controllers
{
    [ApiController]
    [ApiExplorerSettings(GroupName = "Bee")]
    [Route("chunks")]
    [Route("v{api-version:apiVersion}/chunks")]
    public class ChunksController(IChunksControllerService service)
        : ControllerBase
    {
        // Get.

        [HttpGet("{*hash:minlength(1)}")]
        [BeeExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> DownloadChunkAsync(SwarmHash hash) =>
            service.DownloadChunkAsync(hash);
        
        // Head.
        
        [HttpHead("{*hash:minlength(1)}")]
        [BeeExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> GetChunkHeadersAsync(
            SwarmHash hash) =>
            service.GetChunkHeadersAsync(hash);

        // Post.

        [HttpPost]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(SimpleChunkReferenceDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        [RequireAtLeastOneHeader(
            SwarmHttpConsts.SwarmPostageBatchIdHeader,
            SwarmHttpConsts.SwarmPostageStampHeader)]
        [RequestSizeLimit(SwarmCac.SpanDataSize)]
        [Consumes(BeehiveHttpConsts.ApplicationOctetStreamContentType)]
        public Task<IActionResult> UploadChunkAsync(
            [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader)] PostageBatchId? batchId,
            [FromHeader(Name = SwarmHttpConsts.SwarmPostageStampHeader)] PostageStamp? postageStamp,
            [FromBody, Required] Stream dataStream) =>
            service.UploadChunkAsync(dataStream, batchId, postageStamp);

        [Obsolete("Used with BeeTurbo")]
        [HttpPost("~/chunks/bulk-upload")]
        [BeeExceptionFilter]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        [RequestSizeLimit(100 * 1024 * 1024)] //100MB
        [Consumes(BeehiveHttpConsts.ApplicationOctetStreamContentType)]
        public Task<IActionResult> BulkUploadChunksBeeTurboAsync(
            [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader), Required] PostageBatchId batchId,
            [FromBody, Required] Stream dataStream) =>
            service.BulkUploadChunksAsync(dataStream, batchId);

        [HttpPost("~/ev1/chunks/bulk-upload")]
        [BeeExceptionFilter]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        [RequestSizeLimit(100 * 1024 * 1024)] //100MB
        [Consumes(BeehiveHttpConsts.ApplicationOctetStreamContentType)]
        public Task<IActionResult> BulkUploadChunksAsync(
            [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader), Required] PostageBatchId batchId,
            [FromBody, Required] Stream dataStream) =>
            service.BulkUploadChunksAsync(dataStream, batchId);
    }
}