using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Areas.Api.Services;
using Etherna.BeehiveManager.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Controllers
{
    [ApiController]
    [ApiVersion("0.3")]
    [Route("api/v{api-version:apiVersion}/[controller]")]
    public class LoadBalancerController : ControllerBase
    {
        // Fields.
        private readonly ILoadBalancerControllerService service;
        
        // Constructor.
        public LoadBalancerController(ILoadBalancerControllerService service)
        {
            this.service = service;
        }

        // Get.

        /// <summary>
        /// Select best node for download a specific content
        /// </summary>
        /// <param name="hash">Reference hash of the content</param>
        /// <response code="200">Selected Bee node</response>
        [HttpGet("{hash}/nodes")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<BeeNodeDto> SelectDownloadNodeAsync(
            [Required] string hash)
        {
            var beeNode = await service.SelectDownloadNodeAsync(hash);

            // Copy response in headers (Nginx optimization).
            HttpContext.Response.Headers.Add("bee-node-id", beeNode.Id);
            HttpContext.Response.Headers.Add("bee-node-debug-port", beeNode.DebugPort.ToString(CultureInfo.InvariantCulture));
            HttpContext.Response.Headers.Add("bee-node-ethereum-address", beeNode.EthereumAddress);
            HttpContext.Response.Headers.Add("bee-node-gateway-port", beeNode.GatewayPort.ToString(CultureInfo.InvariantCulture));
            HttpContext.Response.Headers.Add("bee-node-hostname", beeNode.Hostname.ToString(CultureInfo.InvariantCulture));
            HttpContext.Response.Headers.Add("bee-node-scheme", beeNode.ConnectionScheme);
            HttpContext.Response.Headers.Add("bee-node-overlay-address", beeNode.OverlayAddress);
            HttpContext.Response.Headers.Add("bee-node-pss-public-key", beeNode.PssPublicKey);
            HttpContext.Response.Headers.Add("bee-node-public-key", beeNode.PublicKey);

            return beeNode;
        }
    }
}
