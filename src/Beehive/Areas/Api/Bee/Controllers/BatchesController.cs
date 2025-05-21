// Copyright 2021-present Etherna SA
// This file is part of Beehive.
// 
// Beehive is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// Beehive is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with Beehive.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.Beehive.Areas.Api.Bee.DtoModels;
using Etherna.Beehive.Areas.Api.Bee.Services;
using Etherna.Beehive.Attributes;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Controllers
{
    [ApiController]
    [ApiExplorerSettings(GroupName = "Bee")]
    [Route("batches")]
    [Route("v{api-version:apiVersion}/batches")]
    public class BatchesController(IBatchesControllerService service)
        : ControllerBase
    {
        // Get.
        
        /// <summary>
        /// Get all global valid postage batches
        /// </summary>
        [HttpGet]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(GlobalPostageBatchesDto), StatusCodes.Status200OK)]
        public Task<IActionResult> GetGlobalValidPostageBatchesAsync() =>
            service.GetGlobalValidPostageBatchesAsync();
    }
}