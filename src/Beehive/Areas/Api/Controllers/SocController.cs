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
    [Route("soc")]
    [Route("v{api-version:apiVersion}/soc")]
    public class SocController(ISocControllerService service)
        : ControllerBase
    {
        // Post.
        
        [HttpPost("{owner:length(40)}/{id}")]
        [SimpleExceptionFilter]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status402PaymentRequired)]
        public Task<IResult> UploadSocAsync(
            string owner,
            string id,
            [FromQuery(Name = "sig"), Required] string signature,
            [FromHeader(Name = SwarmHttpConsts.SwarmPostageBatchIdHeader), Required] PostageBatchId batchId) =>
            service.UploadSocAsync(owner, id, signature, batchId, HttpContext);
    }
}