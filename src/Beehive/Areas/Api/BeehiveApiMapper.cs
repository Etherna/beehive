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

using Etherna.Beehive.Areas.Api.DtoModels;
using Etherna.Beehive.Areas.Api.InputModels;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Etherna.Beehive.Areas.Api
{
    public static class BeehiveApiMapper
    {
        // Methods.
        public static void MapBeehiveApi(this WebApplication app)
        {
            ArgumentNullException.ThrowIfNull(app);
            
            // APIs.
            ConfigureMaps(app.MapGroup("").WithMetadata(new BeehiveApiMarker()));
        }
        
        // Helpers.
        private static void ConfigureMaps(RouteGroupBuilder builder)
        {
            //nodes
            builder.MapGet("/nodes",
                    (IBeehiveApiHandler handler) =>
                        handler.GetBeeNodesAsync())
                .Produces<IEnumerable<BeeNodeDto>>();
            
            builder.MapGet("/nodes/{id}",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id) =>
                        handler.FindByIdAsync(id))
                .Produces<BeeNodeDto>()
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);

            builder.MapPost("/nodes",
                    (IBeehiveApiHandler handler,
                            [Required, FromBody] BeeNodeInput nodeInput) =>
                        handler.AddBeeNodeAsync(nodeInput))
                .Produces(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest);

            builder.MapPut("/nodes/{id}",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id,
                            [Required, FromBody] BeeNodeInput nodeInput) =>
                        handler.UpdateBeeNodeAsync(id, nodeInput))
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            builder.MapDelete("/nodes/{id}",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id) =>
                        handler.RemoveBeeNodeAsync(id))
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
        }
    }
}