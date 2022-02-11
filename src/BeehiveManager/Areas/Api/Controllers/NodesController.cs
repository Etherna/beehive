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
        /// <returns>List of owned postage batches</returns>
        [HttpGet("{id}/batches")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IEnumerable<PostageBatchDto>> GetOwnedPostageBatchesByNodeAsync(
            [Required] string id) =>
            service.GetOwnedPostageBatchesByNodeAsync(id);

        /// <summary>
        /// Find details of a postage batch owned by a node
        /// </summary>
        /// <param name="id">Id of the bee node</param>
        /// <param name="batchId">Postage Batch Id</param>
        /// <returns>Selected postage batch</returns>
        [HttpGet("{id}/batches/{batchId}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<PostageBatchDto> FindPostageBatchOnNodeAsync(
            [Required] string id,
            [Required] string batchId) =>
            service.FindPostageBatchOnNodeAsync(id, batchId);

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

        // Put.

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
    }
}
