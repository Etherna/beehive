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
    [Route("feeds")]
    [Route("v{api-version:apiVersion}/feeds")]
    public class FeedsController(IFeedsControllerService service)
        : ControllerBase
    {
        // Get.

        [HttpGet("{owner}/{topic}")]
        [BeeExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> FindFeedUpdateAsync(
            EthAddress owner,
            SwarmFeedTopic topic,
            [FromQuery] long? at,
            [FromQuery] ulong? after,
            [FromQuery] byte? afterLevel,
            [FromHeader(Name = SwarmHttpConsts.SwarmOnlyRootChunkHeader)] bool onlyRootChunk,
            [FromHeader(Name = SwarmHttpConsts.SwarmFeedLegacyResolveHeader)] bool resolveLegacyPayload,
            [FromQuery] SwarmFeedType type = SwarmFeedType.Sequence) =>
            service.FindFeedUpdateAsync(owner, topic, at, after, afterLevel, type, onlyRootChunk, resolveLegacyPayload, Response);

        // Post.

        [HttpPost("{owner}/{topic}")]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(SimpleChunkReferenceDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        public Task<IActionResult> CreateFeedRootManifestAsync(
            EthAddress owner,
            SwarmFeedTopic topic,
            [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader), Required] PostageBatchId batchId,
            [FromHeader(Name = BeehiveHttpConsts.SwarmCompactLevelHeader)] ushort compactLevel,
            [FromHeader(Name = SwarmHttpConsts.SwarmPinningHeader)] bool pinContent,
            [FromQuery] SwarmFeedType type = SwarmFeedType.Sequence) =>
            service.CreateFeedRootManifestAsync(owner, topic, type, batchId, compactLevel, pinContent);
    }
}