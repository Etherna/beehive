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
using Etherna.Beehive.Areas.Api.Services;
using Etherna.Beehive.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Controllers
{
    [ApiController]
    [ApiVersion("0.3")]
    [Route("api/v{api-version:apiVersion}/[controller]")]
    public class NodesController(INodesControllerService_old service)
        : ControllerBase
    {
        // Get.

        /// <summary>
        /// Check if resource is available from a specific node
        /// </summary>
        /// <param name="id">Id of the bee node</param>
        /// <param name="hash">Resource hash</param>
        /// <returns>True if is available, false oetherwise</returns>
        [HttpGet("{id}/stewardship/{hash}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<bool> CheckResourceAvailabilityFromNodeAsync(
            [Required] string id,
            [Required] string hash) =>
            service.CheckResourceAvailabilityFromNodeAsync(id, hash);

        // Put.

        /// <summary>
        /// Reupload resource to the network
        /// </summary>
        /// <param name="id">Id of the bee node</param>
        /// <param name="hash">Resource hash</param>
        /// <returns>True if is available, false oetherwise</returns>
        [HttpPut("{id}/stewardship/{hash}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task ReuploadResourceToNetworkFromNodeAsync(
            [Required] string id,
            [Required] string hash) =>
            service.ReuploadResourceToNetworkFromNodeAsync(id, hash);
    }
}
