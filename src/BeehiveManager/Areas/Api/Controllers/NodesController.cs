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
    [ApiVersion("0.1")]
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

        // Post.

        /// <summary>
        /// Register a new bee node.
        /// </summary>
        /// <param name="nodeInput">Info of new node</param>
        [HttpPost]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public Task<BeeNodeDto> RegisterAsync(
            [Required] BeeNodeInput nodeInput) =>
            service.AddBeeNodeAsync(nodeInput);

        // Put.

        /// <summary>
        /// Refresh node info from running instance.
        /// </summary>
        /// <param name="nodeId">Id of the bee node</param>
        [HttpPut("{nodeId}/refresh")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        public Task<BeeNodeDto> UpdateAsync(
            string nodeId) =>
            service.RefreshNodeInfoAsync(nodeId);
    }
}
