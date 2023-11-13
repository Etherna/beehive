//   Copyright 2021-present Etherna SA
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
using Etherna.BeehiveManager.Areas.Api.InputModels;
using Etherna.BeehiveManager.Areas.Api.Services;
using Etherna.BeehiveManager.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Controllers
{
    [ApiController]
    [ApiVersion("0.3")]
    [Route("api/v{api-version:apiVersion}/[controller]")]
    public class NodesController : ControllerBase
    {
        // Fields.
        private readonly INodesControllerService service;

        // Constructor.
        public NodesController(INodesControllerService service)
        {
            this.service = service;
        }

        // Get.

        /// <summary>
        /// Get list of registered bee nodes
        /// </summary>
        /// <param name="page">Current page of results</param>
        /// <param name="take">Number of items to retrieve. Max 100</param>
        /// <response code="200">Current page on list</response>
        [HttpGet]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public Task<IEnumerable<BeeNodeDto>> GetBeeNodesAsync(
            [Range(0, int.MaxValue)] int page,
            [Range(1, 100)] int take = 25) =>
            service.GetBeeNodesAsync(page, take);

        /// <summary>
        /// Get node info by its id
        /// </summary>
        /// <param name="id">Id of the bee node</param>
        /// <response code="200">Bee node info</response>
        [HttpGet("{id}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<BeeNodeDto> FindByIdAsync(
            [Required] string id) =>
            service.FindByIdAsync(id);

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
        public Task<IEnumerable<string>> GetPinsByNodeAsync(
            [Required] string id) =>
            service.GetPinsByNodeAsync(id);

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
            [Required, SwarmResourceValidation] string hash) =>
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
            [Required, SwarmResourceValidation] string hash) =>
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
        /// Register a new bee node.
        /// </summary>
        /// <param name="nodeInput">Info of new node</param>
        /// <response code="200">Bee node info</response>
        [HttpPost]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public Task<BeeNodeDto> RegisterAsync(
            [Required] BeeNodeInput nodeInput) =>
            service.AddBeeNodeAsync(nodeInput);

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
            [Required, SwarmResourceValidation] string hash) =>
            service.NotifyPinningOfUploadedContentAsync(id, hash);

        // Put.

        [HttpPut("{id}/config")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task UpdateNodeConfigAsync(
            [Required] string id,
            [Required] UpdateNodeConfigInput config) =>
            service.UpdateNodeConfigAsync(id, config);

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
            [Required, SwarmResourceValidation] string hash) =>
            service.ReuploadResourceToNetworkFromNodeAsync(id, hash);

        // Delete.

        /// <summary>
        /// Remove a bee node.
        /// </summary>
        /// <param name="id">Id of the bee node</param>
        [HttpDelete("{id}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task RemoveAsync(
            [Required] string id) =>
            service.RemoveBeeNodeAsync(id);

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
            [Required, SwarmResourceValidation] string hash) =>
            service.DeletePinAsync(id, hash);
    }
}
