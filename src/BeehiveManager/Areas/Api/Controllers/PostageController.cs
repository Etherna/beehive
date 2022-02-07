using Etherna.BeehiveManager.Areas.Api.Services;
using Etherna.BeehiveManager.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
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

        // Post.

        /// <summary>
        /// Buy a new postage batch
        /// </summary>
        /// <param name="depth">Batch depth</param>
        /// <param name="plurAmount">Amount of BZZ in Plur added that the postage batch will have</param>
        /// <param name="gasPrice">Gas price for transaction</param>
        /// <param name="immutable">Is batch immutable</param>
        /// <param name="label">An optional label for this batch</param>
        /// <param name="nodeId">Bee node Id</param>
        /// <returns>Postage batch id</returns>
        [HttpPost("batches")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public Task<string> BuyPostageBatchAsync(
            int depth,
            int plurAmount,
            int? gasPrice = null,
            bool immutable = false,
            string? label = null,
            string? nodeId = null) =>
            service.BuyPostageBatchAsync(depth, plurAmount, gasPrice, immutable, label, nodeId);

        // Put.

        // Patch.

        // Delete.
    }
}
