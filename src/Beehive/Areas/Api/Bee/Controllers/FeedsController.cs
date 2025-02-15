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

using Etherna.Beehive.Areas.Api.Bee.Services;
using Etherna.Beehive.Attributes;
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

        [HttpGet("{owner:length(40)}/{topic}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IResult> FindFeedUpdateAsync(string owner, string topic) =>
            service.FindFeedUpdateAsync(owner, topic, HttpContext);

        // Post.

        [HttpPost("{owner:length(40)}/{topic}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        public Task<IResult> CreateFeedRootManifestAsync(
            string owner,
            string topic,
            [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader), Required] PostageBatchId batchId) =>
            service.CreateFeedRootManifestAsync(owner, topic, batchId, HttpContext);
    }
}