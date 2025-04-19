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
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Controllers
{
    [ApiController]
    [ApiExplorerSettings(GroupName = "Bee")]
    [Route("stamps")]
    [Route("v{api-version:apiVersion}/stamps")]
    public class StampsController(IStampsControllerService service)
        : ControllerBase
    {
        // Get.
        
        /// <summary>
        /// Get all owned postage batches
        /// </summary>
        [HttpGet]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(PostageBatchStampListDto), StatusCodes.Status200OK)]
        public Task<IActionResult> GetOwnedPostageBatchesAsync() =>
            service.GetOwnedPostageBatchesAsync();

        /// <summary>
        /// Get details of a postage batch
        /// </summary>
        /// <param name="batchId">Postage Batch Id</param>
        [HttpGet("{batchId}")]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(PostageBatchDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> GetPostageBatchAsync(
            PostageBatchId batchId) =>
            service.GetPostageBatchAsync(batchId);
        
        [HttpGet("{batchId}/buckets")]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(PostageBatchBucketsDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> GetPostageBatchBucketsAsync(
            PostageBatchId batchId) =>
            service.GetPostageBatchBucketsAsync(batchId);
        
        // Patch.

        [HttpPatch("dilute/{batchId}/{depth}")]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(PostageBatchIdWithTxHashDto), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public Task<IActionResult> DilutePostageBatchAsync(
            PostageBatchId batchId,
            int depth,
            [FromHeader(Name = SwarmHttpConsts.GasLimitHeader)] ulong? gasLimit = null,
            [FromHeader(Name = SwarmHttpConsts.GasPriceHeader)] XDaiBalance? gasPrice = null) =>
            service.DilutePostageBatchAsync(batchId, depth, gasLimit, gasPrice);

        [HttpPatch("topup/{batchId}/{amount}")]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(PostageBatchIdWithTxHashDto), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public Task<IActionResult> TopUpPostageBatchAsync(
            PostageBatchId batchId,
            BzzBalance amount,
            [FromHeader(Name = SwarmHttpConsts.GasLimitHeader)] ulong? gasLimit = null,
            [FromHeader(Name = SwarmHttpConsts.GasPriceHeader)] XDaiBalance? gasPrice = null) =>
            service.TopUpPostageBatchAsync(batchId, amount, gasLimit, gasPrice);
        
        // Post.

        /// <summary>
        /// Buy a new postage batch
        /// </summary>
        /// <param name="amount">Amount of BZZ in Plur added that the postage batch will have</param>
        /// <param name="depth">Batch depth</param>
        /// <param name="label">An optional label for this batch</param>
        /// <param name="immutable">Is batch immutable</param>
        /// <param name="gasLimit">Ethereum tx gas limit</param>
        /// <param name="gasPrice">Ethereum tx gas price</param>
        [HttpPost("{amount}/{depth}")]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(PostageBatchIdWithTxHashDto), StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        public Task<IActionResult> BuyPostageBatchAsync(
            BzzBalance amount,
            int depth,
            [FromQuery] string? label = null,
            [FromHeader(Name = SwarmHttpConsts.ImmutableHeader)] bool immutable = false,
            [FromHeader(Name = SwarmHttpConsts.GasLimitHeader)] ulong? gasLimit = null,
            [FromHeader(Name = SwarmHttpConsts.GasPriceHeader)] XDaiBalance? gasPrice = null) =>
            service.BuyPostageBatchAsync(amount, depth, label, immutable, gasLimit, gasPrice);
    }
}