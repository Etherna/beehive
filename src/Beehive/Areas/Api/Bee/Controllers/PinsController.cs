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
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Controllers
{
    [ApiController]
    [ApiExplorerSettings(GroupName = "Bee")]
    [Route("pins")]
    [Route("v{api-version:apiVersion}/pins")]
    public class PinsController(IPinsControllerService service)
        : ControllerBase
    {
        // Get.
        
        [HttpGet]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(BeePinsDto), StatusCodes.Status200OK)]
        public Task<IActionResult> GetPinsBeeAsync() =>
            service.GetPinsBeeAsync();
        
        [HttpGet("~/ev1/pins")]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(IEnumerable<BeehivePinDto>), StatusCodes.Status200OK)]
        public Task<IActionResult> GetPinsBeehiveAsync(
            [Range(0, int.MaxValue)] int page,
            [Range(1, 10000)] int take = 500) =>
            service.GetPinsBeehiveAsync(page, take);
        
        [HttpGet("{*hash:minlength(1)}")]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(SimpleChunkReferenceDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> GetPinStatusBeeAsync(
            SwarmHash hash) =>
            service.GetPinStatusBeeAsync(hash);
        
        [HttpGet("~/ev1/pins/{*hash:minlength(1)}")]
        [BeeExceptionFilter]
        [ProducesResponseType(typeof(BeehivePinDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> GetPinStatusBeehiveAsync(
            SwarmHash hash) =>
            service.GetPinStatusBeehiveAsync(hash);
        
        // Post.

        [HttpPost("{hash}")]
        [BeeExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public Task<IActionResult> CreatePinBeeAsync(SwarmHash hash) =>
            service.CreatePinBeeAsync(hash);

        [HttpPost("~/ev1/pins/{hash}")]
        [BeeExceptionFilter]
        [ProducesResponseType(StatusCodes.Status200OK)]
        public Task CreatePinBeehiveAsync(SwarmHash hash) =>
            service.CreatePinBeehiveAsync(hash);
    }
}