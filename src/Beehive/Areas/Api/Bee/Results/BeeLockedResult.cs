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
using Microsoft.AspNetCore.Mvc;

namespace Etherna.Beehive.Areas.Api.Bee.Results
{
    public class BeeLockedResult : JsonResult
    {
        private const int DefaultStatusCode = 423;
        public BeeLockedResult(string? message = null)
            : base(new BeeErrorDto(DefaultStatusCode, message ?? "Locked"))
        {
            StatusCode = DefaultStatusCode;
        }
    }
}