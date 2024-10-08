// Copyright 2021-present Etherna SA
// This file is part of BeehiveManager.
// 
// BeehiveManager is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeehiveManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeehiveManager.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Areas.Api.Services;
using Etherna.BeehiveManager.Attributes;
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Controllers
{
    [ApiController]
    [ApiVersion("0.3")]
    [Route("api/v{api-version:apiVersion}/[controller]")]
    public class PostageController : ControllerBase
    {
        // Fields.
        private readonly ILoadBalancerControllerService loadBalancerService;
        private readonly IPostageControllerService service;

        // Constructor.
        public PostageController(
            ILoadBalancerControllerService loadBalancerService,
            IPostageControllerService service)
        {
            this.loadBalancerService = loadBalancerService;
            this.service = service;
        }

        // Get.

        /// <summary>
        /// Find bee node info by an owned postage batch Id
        /// </summary>
        /// <param name="id">Id of the postage batch</param>
        /// <response code="200">Bee node info</response>
        [HttpGet("batches/{id}/node")]
        [Obsolete("Use instead API in LoadBalancerController")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<BeeNodeDto> FindBeeNodeOwnerOfPostageBatchAsync(
            [Required] string id)
        {
            var beeNodeInfo = await loadBalancerService.FindBeeNodeOwnerOfPostageBatchAsync(id);

            // Copy response in headers (Nginx optimization).
            HttpContext.Response.Headers.Append("bee-node-id", beeNodeInfo.Id);
            HttpContext.Response.Headers.Append("bee-node-gateway-port", beeNodeInfo.GatewayPort.ToString(CultureInfo.InvariantCulture));
            HttpContext.Response.Headers.Append("bee-node-hostname", beeNodeInfo.Hostname.ToString(CultureInfo.InvariantCulture));
            HttpContext.Response.Headers.Append("bee-node-scheme", beeNodeInfo.ConnectionScheme);

            return beeNodeInfo;
        }

        // Post.

        /// <summary>
        /// Buy a new postage batch
        /// </summary>
        /// <param name="amount">Amount of BZZ in Plur added that the postage batch will have</param>
        /// <param name="depth">Batch depth</param>
        /// <param name="immutable">Is batch immutable</param>
        /// <param name="label">An optional label for this batch</param>
        /// <param name="nodeId">Bee node Id</param>
        /// <response code="200">Postage batch id</response>
        [HttpPost("batches")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<PostageBatchRefDto> BuyPostageBatchAsync(
            long amount,
            int depth,
            bool immutable = false,
            string? label = null,
            string? nodeId = null) =>
            service.BuyPostageBatchAsync(BzzBalance.FromPlurLong(amount), depth, immutable, label, nodeId);

        // Put.

        // Patch.

        [HttpPatch("batches/{id}/dilute/{depth}")]
        [SimpleExceptionFilter]
        [Produces("application/json")] //force because of https://github.com/RicoSuter/NSwag/issues/4132
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<string> DilutePostageBatchAsync(
            [Required] string id,
            [Required] int depth) =>
            (await service.DilutePostageBatchAsync(id, depth)).ToString();

        [HttpPatch("batches/{id}/topup/{amount}")]
        [SimpleExceptionFilter]
        [Produces("application/json")] //force because of https://github.com/RicoSuter/NSwag/issues/4132
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<string> TopUpPostageBatchAsync(
            [Required] string id,
            [Required] long amount) =>
            (await service.TopUpPostageBatchAsync(id, BzzBalance.FromPlurLong(amount))).ToString();

        // Delete.
    }
}
