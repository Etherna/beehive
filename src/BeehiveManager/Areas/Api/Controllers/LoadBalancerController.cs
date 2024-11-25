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
    public class LoadBalancerController(ILoadBalancerControllerService service)
        : ControllerBase
    {
        // Get.

        /// <summary>
        /// Select a healthy bee node
        /// </summary>
        /// <response code="200">Bee node info</response>
        [HttpGet]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<BeeNodeDto> SelectHealthyNodeAsync()
        {
            var beeNode = await service.SelectHealthyNodeAsync();
            WriteNodeInfoInHeaders(beeNode); //nginx optimization
            return beeNode;
        }

        /// <summary>
        /// Find bee node info by an owned postage batch Id
        /// </summary>
        /// <param name="id">Id of the postage batch</param>
        /// <response code="200">Bee node info</response>
        [HttpGet("batch/{id}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<BeeNodeDto> FindBeeNodeOwnerOfPostageBatchAsync(
            [Required] string id)
        {
            var beeNode = await service.FindBeeNodeOwnerOfPostageBatchAsync(id);
            WriteNodeInfoInHeaders(beeNode); //nginx optimization
            return beeNode;
        }

        /// <summary>
        /// Select best node for download a specific content
        /// </summary>
        /// <param name="hash">Reference hash of the content</param>
        /// <response code="200">Selected Bee node</response>
        [HttpGet("download/{hash}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<BeeNodeDto> SelectDownloadNodeAsync(
            [Required, SwarmResourceValidation] string hash)
        {
            var beeNode = await service.SelectDownloadNodeAsync(hash);
            WriteNodeInfoInHeaders(beeNode); //nginx optimization
            return beeNode;
        }

        // Helpers.
        private void WriteNodeInfoInHeaders(BeeNodeDto beeNode)
        {
            HttpContext.Response.Headers.Append("bee-node-id", beeNode.Id);
            HttpContext.Response.Headers.Append("bee-node-gateway-port", beeNode.GatewayPort.ToString(CultureInfo.InvariantCulture));
            HttpContext.Response.Headers.Append("bee-node-hostname", beeNode.Hostname.ToString(CultureInfo.InvariantCulture));
            HttpContext.Response.Headers.Append("bee-node-scheme", beeNode.ConnectionScheme);
        }
    }
}
