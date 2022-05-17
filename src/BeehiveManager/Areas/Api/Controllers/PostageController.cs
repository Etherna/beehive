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
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Controllers
{
    [ApiController]
    [ApiVersion("0.3")]
    [Route("api/v{api-version:apiVersion}/[controller]")]
    public class PostageController : ControllerBase
    {
        // Fields.
        private readonly IPostageControllerService service;

        // Constructor.
        public PostageController(
            IPostageControllerService service)
        {
            this.service = service;
        }

        // Get.

        /// <summary>
        /// Find bee node info by an owned postage batch Id
        /// </summary>
        /// <param name="id">Id of the postage batch</param>
        /// <response code="200">Bee node info</response>
        [HttpGet("batches/{id}/node")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<BeeNodeDto> FindBeeNodeOwnerOfPostageBatchAsync(
            [Required] string id) =>
            service.FindBeeNodeOwnerOfPostageBatchAsync(id);

        /// <summary>
        /// Find bee node url by an owned postage batch Id (this simplify need of json parse by nginx calls)
        /// </summary>
        /// <param name="id">Id of the postage batch</param>
        /// <param name="useHeader">True if response is wanted in header</param>
        /// <response code="200">Bee node url</response>
        [HttpGet("batches/{id}/node/url")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> FindBeeNodeUrlOwnerOfPostageBatchAsync(
            [Required] string id,
            bool useHeader = false)
        {
            var url = (await service.FindBeeNodeOwnerOfPostageBatchAsync(id)).Url.ToString();
            if (useHeader)
            {
                HttpContext.Response.Headers.Add("bee-node-url", url);
                return new OkResult();
            }
            else
                return new ContentResult() { Content = url, StatusCode = 200 };
        }

        // Post.

        /// <summary>
        /// Buy a new postage batch
        /// </summary>
        /// <param name="amount">Amount of BZZ in Plur added that the postage batch will have</param>
        /// <param name="depth">Batch depth</param>
        /// <param name="gasPrice">Gas price for transaction</param>
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
            long? gasPrice = null,
            bool immutable = false,
            string? label = null,
            string? nodeId = null) =>
            service.BuyPostageBatchAsync(amount, depth, gasPrice, immutable, label, nodeId);

        // Put.

        // Patch.

        // Delete.
    }
}
