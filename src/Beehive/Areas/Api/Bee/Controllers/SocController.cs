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
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Controllers
{
    [ApiController]
    [ApiExplorerSettings(GroupName = "Bee")]
    [Route("soc")]
    [Route("v{api-version:apiVersion}/soc")]
    public class SocController(ISocControllerService service)
        : ControllerBase
    {
        // Get.
        [HttpGet("{owner}/{id}")]
        [BeeExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> ResolveSocAsync(
            EthAddress owner,
            SwarmSocIdentifier id,
            [FromHeader(Name = SwarmHttpConsts.SwarmOnlyRootChunkHeader)] bool onlyRootChunk) =>
            service.ResolveSocAsync(owner, id, onlyRootChunk, HttpContext.Response);
        
        // Post.
        
        [HttpPost("{owner}/{id}")]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(SimpleChunkReferenceDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        [RequireAtLeastOneHeader(
            SwarmHttpConsts.SwarmPostageBatchIdHeader,
            SwarmHttpConsts.SwarmPostageStampHeader)]
        [RequestSizeLimit(SwarmCac.SpanDataSize)]
        [Consumes("application/octet-stream")]
        public Task<IActionResult> UploadSocAsync(
            EthAddress owner,
            SwarmSocIdentifier id,
            [FromQuery(Name = "sig"), Required] string signature,
            [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader)] PostageBatchId? batchId,
            [FromHeader(Name = SwarmHttpConsts.SwarmPostageStampHeader)] PostageStamp? postageStamp,
            [FromHeader(Name = SwarmHttpConsts.SwarmPinningHeader)] bool pinContent,
            [FromBody, Required] Stream bodyStream) =>
            service.UploadSocAsync(
                owner,
                id,
                signature,
                batchId,
                postageStamp,
                bodyStream,
                pinContent);
    }
}