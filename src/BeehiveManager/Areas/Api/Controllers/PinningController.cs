//   Copyright 2021-present Etherna Sagl
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Areas.Api.Services;
using Etherna.BeehiveManager.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Controllers
{
    [ApiController]
    [ApiVersion("0.3")]
    [Route("api/v{api-version:apiVersion}/[controller]")]
    public class PinningController : ControllerBase
    {
        // Fields.
        private readonly IPinningControllerService service;

        // Constructor.
        public PinningController(IPinningControllerService service)
        {
            this.service = service;
        }

        // Get.

        /// <summary>
        /// Find bee node pinning a specific content
        /// </summary>
        /// <param name="hash">Reference hash of the content</param>
        /// <param name="requireAliveNodes">True if nodes needs to be alive</param>
        /// <response code="200">List of Bee nodes</response>
        [HttpGet("{hash}/nodes")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IEnumerable<BeeNodeDto>> FindBeeNodesPinningContentAsync(
            [Required] string hash, bool requireAliveNodes)
        {
            var beeNodes = await service.FindBeeNodesPinningContentAsync(hash, requireAliveNodes);

            // Copy response in headers (Nginx optimization).
            HttpContext.Response.Headers.Add("bee-node-id", beeNodes.Select(n => n.Id).ToArray());
            HttpContext.Response.Headers.Add("bee-node-debug-port", beeNodes.Select(n => n.DebugPort.ToString(CultureInfo.InvariantCulture)).ToArray());
            HttpContext.Response.Headers.Add("bee-node-gateway-port", beeNodes.Select(n => n.GatewayPort.ToString(CultureInfo.InvariantCulture)).ToArray());
            HttpContext.Response.Headers.Add("bee-node-hostname", beeNodes.Select(n => n.Hostname.ToString(CultureInfo.InvariantCulture)).ToArray());
            HttpContext.Response.Headers.Add("bee-node-scheme", beeNodes.Select(n => n.ConnectionScheme).ToArray());

            return beeNodes;
        }

        // Post.

        /// <summary>
        /// Pin a content into a node that doesn't already pin it
        /// </summary>
        /// <param name="hash">The content hash reference</param>
        /// <param name="nodeId">Bee node Id</param>
        /// <response code="200">Id of the new pinning node</response>
        [HttpPost]
        [SimpleExceptionFilter]
        [Produces("application/json")] //force because of https://github.com/RicoSuter/NSwag/issues/4132
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<string> PinContentInNodeAsync(
            [Required] string hash, string? nodeId = null) =>
            service.PinContentInNodeAsync(hash, nodeId);
    }
}
