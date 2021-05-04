using Etherna.BeehiveManager.Areas.Api.Services;
using Microsoft.AspNetCore.Mvc;

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
    }
}
