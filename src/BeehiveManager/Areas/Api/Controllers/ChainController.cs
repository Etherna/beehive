﻿// Copyright 2021-present Etherna SA
// This file is part of BeehiveManager.
// 
// BeehiveManager is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeehiveManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeehiveManager.
// If not, see <https://www.gnu.org/licenses/>.

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
