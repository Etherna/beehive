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

using Asp.Versioning;
using Etherna.Beehive.Areas.Api.DtoModels;
using Etherna.Beehive.Areas.Api.Services;
using Etherna.Beehive.Attributes;
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Controllers
{
    [ApiController]
    [ApiVersion("0.3")]
    [Route("api/v{api-version:apiVersion}/[controller]")]
    public class PostageController(
        IPostageControllerService service)
        : ControllerBase
    {
        // Patch.

        [HttpPatch("batches/{id}/dilute/{depth}")]
        [SimpleExceptionFilter]
        [Produces("application/json")] //force because of https://github.com/RicoSuter/NSwag/issues/4132
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<string> DilutePostageBatchAsync(
            [Required] string id,
            [Required] int depth) =>
            (await service.DilutePostageBatchAsync(id, depth)).ToString();

        [HttpPatch("batches/{id}/topup/{amount}")]
        [SimpleExceptionFilter]
        [Produces("application/json")] //force because of https://github.com/RicoSuter/NSwag/issues/4132
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<string> TopUpPostageBatchAsync(
            [Required] string id,
            [Required] long amount) =>
            (await service.TopUpPostageBatchAsync(id, BzzBalance.FromPlurLong(amount))).ToString();
    }
}
