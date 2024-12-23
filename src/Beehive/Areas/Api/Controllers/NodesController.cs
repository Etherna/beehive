﻿// Copyright 2021-present Etherna SA
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
using System.Linq;
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
        /// Get all postage batches owned by a node
        /// </summary>
        /// <param name="id">Id of the bee node</param>
        /// <response code="200">List of owned postage batches</response>
        [HttpGet("{id}/batches")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IEnumerable<PostageBatchDto>> GetPostageBatchesByNodeAsync(
            [Required] string id) =>
            service.GetPostageBatchesByNodeAsync(id);

        /// <summary>
        /// Get details of a postage batch owned by a node
        /// </summary>
        /// <param name="id">Id of the bee node</param>
        /// <param name="batchId">Postage Batch Id</param>
        /// <response code="200">Selected postage batch</response>
        [HttpGet("{id}/batches/{batchId}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<PostageBatchDto> GetPostageBatchDetailsAsync(
            [Required] string id,
            [Required] string batchId) =>
            service.GetPostageBatchDetailsAsync(id, batchId);

        /// <summary>
        /// Get all pinned resources by a node
        /// </summary>
        /// <param name="id">Id of the bee node</param>
        /// <response code="200">List of pinned resources</response>
        [HttpGet("{id}/pins")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IEnumerable<string>> GetPinsByNodeAsync(
            [Required] string id) =>
            (await service.GetPinsByNodeAsync(id)).Select(h => h.ToString());

        /// <summary>
        /// Get details of a pinned resource on a node
        /// </summary>
        /// <param name="id">Id of the bee node</param>
        /// <param name="hash">Resource hash</param>
        /// <returns>Pinned resource info</returns>
        [HttpGet("{id}/pins/{hash}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<PinnedResourceDto> GetPinDetailsAsync(
            [Required] string id,
            [Required] string hash) =>
            service.GetPinDetailsAsync(id, hash);

        /// <summary>
        /// Get live status of a Bee node
        /// </summary>
        /// <param name="id">Id of the bee node</param>
        /// <response code="200">Live status of the node</response>
        [HttpGet("{id}/status")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<BeeNodeStatusDto> GetBeeNodeLiveStatusAsync(
            [Required] string id) =>
            service.GetBeeNodeLiveStatusAsync(id);

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

        /// <summary>
        /// Get live status of all Bee node
        /// </summary>
        /// <response code="200">Live status of all nodes</response>
        [HttpGet("status")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public IEnumerable<BeeNodeStatusDto> GetAllBeeNodeLiveStatus() =>
            service.GetAllBeeNodeLiveStatus();

        // Post.

        /// <summary>
        /// Notify live manager of pinned content during upload
        /// </summary>
        /// <param name="id">Id of the bee node</param>
        /// <param name="hash">Resource hash</param>
        [HttpPost("{id}/pins/{hash}/uploaded")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task NotifyPinningOfUploadedContentAsync(
            [Required] string id,
            [Required] string hash) =>
            service.NotifyPinningOfUploadedContentAsync(id, hash);

        // Put.

        /// <summary>
        /// Force full status refresh on a Bee node
        /// </summary>
        /// <param name="id">Id of the bee node</param>
        /// <response code="200">True if node was alive</response>
        [HttpPut("{id}/status")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<bool> ForceFullStatusRefreshAsync(
            [Required] string id) =>
            service.ForceFullStatusRefreshAsync(id);

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

        // Delete.

        /// <summary>
        /// Delete a pinned resource from a node
        /// </summary>
        /// <param name="id">Id of the bee node</param>
        /// <param name="hash">Resource hash</param>
        [HttpDelete("{id}/pins/{hash}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task DeletePinAsync(
            [Required] string id,
            [Required] string hash) =>
            service.DeletePinAsync(id, hash);
    }
}
