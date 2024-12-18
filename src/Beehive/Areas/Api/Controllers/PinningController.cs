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

using Asp.Versioning;
using Etherna.Beehive.Areas.Api.DtoModels;
using Etherna.Beehive.Areas.Api.Services;
using Etherna.Beehive.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Controllers
{
    [ApiController]
    [ApiVersion("0.3")]
    [Route("api/v{api-version:apiVersion}/[controller]")]
    public class PinningController(IPinningControllerService service) : ControllerBase
    {
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
            [Required, SwarmResourceValidation] string hash,
            bool requireAliveNodes)
        {
            var beeNodes = await service.FindBeeNodesPinningContentAsync(hash, requireAliveNodes);

            // Copy response in headers (Nginx optimization).
            HttpContext.Response.Headers.Append("bee-node-id", beeNodes.Select(n => n.Id).ToArray());
            HttpContext.Response.Headers.Append("bee-node-gateway-port", beeNodes.Select(n => n.GatewayPort.ToString(CultureInfo.InvariantCulture)).ToArray());
            HttpContext.Response.Headers.Append("bee-node-hostname", beeNodes.Select(n => n.Hostname.ToString(CultureInfo.InvariantCulture)).ToArray());
            HttpContext.Response.Headers.Append("bee-node-scheme", beeNodes.Select(n => n.ConnectionScheme).ToArray());

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
