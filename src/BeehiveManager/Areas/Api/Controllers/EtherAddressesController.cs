using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Controllers
{
    [Obsolete("This is a dropped feature")]
    [ApiController]
    [ApiVersion("0.3")]
    [Route("api/v{api-version:apiVersion}/[controller]")]
    public class EtherAddressesController : ControllerBase
    {
        // Get.

        /// <summary>
        /// Get ethereum address configuration
        /// </summary>
        /// <param name="address">The ethereum address</param>
        /// <response code="200">Ethereum address configuration</response>
        [HttpGet("{address}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<EtherAddressDto> FindEtherAddressConfigAsync([Required] string address) =>
            Task.FromResult(new EtherAddressDto(address));

        // Patch.

        /// <summary>
        /// Set preferred SOC node for address
        /// </summary>
        [HttpGet("{address}/socnode/{nodeId}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task SetPreferredSocNodeAsync() =>
            Task.CompletedTask;
    }
}
