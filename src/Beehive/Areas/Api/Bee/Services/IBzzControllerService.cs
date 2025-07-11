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

using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public interface IBzzControllerService
    {
        Task<IActionResult> DownloadBzzAsync(
            string strAddress,
            HttpContext httpContext);
        
        Task<IActionResult> GetBzzHeadersAsync(
            string strAddress,
            HttpContext httpContext);

        Task<IActionResult> UploadBzzAsync(
            HttpRequest request,
            string? name,
            PostageBatchId batchId,
            ushort compactLevel,
            bool pinContent,
            string contentType,
            bool isDirectory,
            string? indexDocument,
            string? errorDocument);
    }
}