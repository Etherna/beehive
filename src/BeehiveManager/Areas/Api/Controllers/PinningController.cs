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
            HttpContext.Response.Headers.Add("bee-node-ethereum-address", beeNodes.Select(n => n.EthereumAddress).ToArray());
            HttpContext.Response.Headers.Add("bee-node-gateway-port", beeNodes.Select(n => n.GatewayPort.ToString(CultureInfo.InvariantCulture)).ToArray());
            HttpContext.Response.Headers.Add("bee-node-hostname", beeNodes.Select(n => n.Hostname.ToString(CultureInfo.InvariantCulture)).ToArray());
            HttpContext.Response.Headers.Add("bee-node-scheme", beeNodes.Select(n => n.ConnectionScheme).ToArray());
            HttpContext.Response.Headers.Add("bee-node-overlay-address", beeNodes.Select(n => n.OverlayAddress).ToArray());
            HttpContext.Response.Headers.Add("bee-node-pss-public-key", beeNodes.Select(n => n.PssPublicKey).ToArray());
            HttpContext.Response.Headers.Add("bee-node-public-key", beeNodes.Select(n => n.PublicKey).ToArray());

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
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<string> PinContentInNodeAsync(
            string hash,
            string? nodeId = null) =>
            service.PinContentInNodeAsync(hash, nodeId);
    }
}
