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

using Etherna.Beehive.Areas.Api.InputModels;
using Microsoft.AspNetCore.Http;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api
{
    public interface IBeehiveApiHandler
    {
        Task<IResult> AddBeeNodeAsync(BeeNodeInput nodeInput);
        Task<IResult> FindByIdAsync(string id);
        Task<IResult> GetBeeNodesAsync();
        Task<IResult> RemoveBeeNodeAsync(string id);
        Task<IResult> UpdateBeeNodeAsync(string id, BeeNodeInput nodeInput);
    }
}