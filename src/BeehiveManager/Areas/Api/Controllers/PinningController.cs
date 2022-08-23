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
    public class PinningController : ControllerBase
    {
        // Fields.
        private readonly IPinningControllerService service;

        // Constructor.
        public PinningController(IPinningControllerService service)
        {
            this.service = service;
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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<string> PinContentInNodeAsync(
            string hash,
            string? nodeId = null) =>
            service.PinContentInNodeAsync(hash, nodeId);
    }
}
