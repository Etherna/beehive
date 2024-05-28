// Copyright 2021-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

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
    public class LoadBalancerController : ControllerBase
    {
        // Fields.
        private readonly ILoadBalancerControllerService service;
        
        // Constructor.
        public LoadBalancerController(ILoadBalancerControllerService service)
        {
            this.service = service;
        }

        // Get.

        /// <summary>
        /// Select an healthy bee node
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

        /// <summary>
        /// Select best node for download a specific content
        /// </summary>
        /// <response code="200">Selected Bee node</response>
        [HttpGet("soc/{address}")]
        [Obsolete("Dropped feature, no more necessary")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<BeeNodeDto> SelectSocNodeAsync()
        {
            var beeNode = await service.SelectHealthyNodeAsync();
            WriteNodeInfoInHeaders(beeNode); //nginx optimization
            return beeNode;
        }

        // Helpers.
        private void WriteNodeInfoInHeaders(BeeNodeDto beeNode)
        {
            HttpContext.Response.Headers.Add("bee-node-id", beeNode.Id);
            HttpContext.Response.Headers.Add("bee-node-gateway-port", beeNode.GatewayPort.ToString(CultureInfo.InvariantCulture));
            HttpContext.Response.Headers.Add("bee-node-hostname", beeNode.Hostname.ToString(CultureInfo.InvariantCulture));
            HttpContext.Response.Headers.Add("bee-node-scheme", beeNode.ConnectionScheme);
        }
    }
}
