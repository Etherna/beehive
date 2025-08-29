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
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Controllers
{
    [ApiController]
    [ApiExplorerSettings(GroupName = "Bee")]
    [Route("bzz")]
    [Route("v{api-version:apiVersion}/bzz")]
    public class BzzController(IBzzControllerService service)
        : ControllerBase
    {
        // Get.

        [HttpGet("{*address:minlength(1)}")]
        [BeeExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> DownloadBzzAsync(string address) =>
            //receive address as a raw string, we need to redirect in case it is only an <hash> without final '/'
            service.DownloadBzzAsync(address, HttpContext);
        
        // Head.
        
        [HttpHead("{*address:minlength(1)}")]
        [BeeExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> GetBzzHeadersAsync(string address) =>
            service.GetBzzHeadersAsync(address, HttpContext);
        
        // Post.
        
        [HttpPost]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(ChunkReferenceDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ConsumesUnrestricted(
            BeehiveHttpConsts.ApplicationOctetStreamContentType,
            BeehiveHttpConsts.ApplicationTarContentType,
            BeehiveHttpConsts.MultiPartFormDataContentType)]
        public Task<IActionResult> UploadBzzAsync(
            [FromQuery] string? name,
            [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader), Required] PostageBatchId batchId,
            [FromHeader(Name = BeehiveHttpConsts.SwarmCompactLevelHeader)] ushort compactLevel,
            [FromHeader(Name = SwarmHttpConsts.SwarmEncryptHeader)] bool encrypt,
            [FromHeader(Name = SwarmHttpConsts.SwarmPinningHeader)] bool pinContent,
            [FromHeader(Name = SwarmHttpConsts.ContentTypeHeader), Required] string contentType,
            [FromHeader(Name = SwarmHttpConsts.SwarmCollectionHeader)] bool isDirectory,
            [FromHeader(Name = SwarmHttpConsts.SwarmIndexDocumentHeader)] string? indexDocument,
            [FromHeader(Name = SwarmHttpConsts.SwarmErrorDocumentHeader)] string? errorDocument) =>
            service.UploadBzzAsync(
                HttpContext.Request,
                name,
                batchId,
                compactLevel,
                encrypt,
                pinContent,
                contentType,
                isDirectory,
                indexDocument,
                errorDocument);
    }
}