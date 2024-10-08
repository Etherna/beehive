// Copyright 2021-present Etherna SA
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

using Etherna.MongODM.AspNetCore.UI.Auth.Filters;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Configs.MongODM
{
    public class AllowAllFilter : IDashboardAuthFilter
    {
        public Task<bool> AuthorizeAsync(HttpContext? context) => Task.FromResult(true);
    }
}
