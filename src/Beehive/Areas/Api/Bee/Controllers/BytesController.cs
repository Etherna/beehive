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
    [Route("bytes")]
    [Route("v{api-version:apiVersion}/bytes")]
    public class BytesController(IBytesControllerService service)
        : ControllerBase
    {
        // Get.
        
        [HttpGet("{*hash:minlength(1)}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> DownloadBytesAsync(
            SwarmHash hash,
            [FromHeader(Name = BeehiveHttpConsts.SwarmEncryptionKey)] XorEncryptKey? encryptionKey,
            [FromHeader(Name = BeehiveHttpConsts.SwarmRecursiveEncryption)] bool recursiveEncryption) =>
            service.DownloadBytesAsync(hash, encryptionKey, recursiveEncryption);
        
        // Post.

        [HttpPost]
        [SimpleExceptionFilter]
        [ProducesResponseType(typeof(ChunkReferenceDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        public Task<IActionResult> UploadBytesAsync(
            [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader), Required] PostageBatchId batchId,
            [FromHeader(Name = BeehiveHttpConsts.SwarmCompactLevelHeader)] ushort compactLevel,
            [FromHeader(Name = SwarmHttpConsts.SwarmPinningHeader)] bool pinContent) =>
            service.UploadBytesAsync(batchId, compactLevel, pinContent, HttpContext);
    }
}