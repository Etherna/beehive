using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Areas.Api.Services;
using Etherna.BeehiveManager.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Etherna.BeehiveManager.Areas.Api.Controllers
{
    [ApiController]
    [ApiVersion("0.3")]
    [Route("api/v{api-version:apiVersion}/[controller]")]
    public class ChainController : ControllerBase
    {
        // Fields.
        private readonly IChainControllerService service;

        // Constructor.
        public ChainController(IChainControllerService service)
        {
            this.service = service;
        }

        // Get.

        /// <summary>
        /// Get chain state
        /// </summary>
        /// <response code="200">Last valid chain state</response>
        [HttpGet("state")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public ChainStateDto? GetChainState() =>
            service.GetChainState();
    }
}
