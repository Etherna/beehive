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
using Etherna.SwarmSdk.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
                .WithDescription("Every bee node registered in beehive, with its live status: alive, heartbeat errors, addresses.")
                .Produces<IEnumerable<BeeNodeDto>>();
            
            builder.MapGet("/nodes/{id}",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id) =>
                        handler.FindByIdAsync(id))
                .WithDescription("A bee node registered in beehive, by id, with its live status.")
                .Produces<BeeNodeDto>()
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);

            builder.MapPost("/nodes",
                    (IBeehiveApiHandler handler,
                            [Required, FromBody] BeeNodeInput nodeInput) =>
                        handler.AddBeeNodeAsync(nodeInput))
                .WithDescription("Registers a bee node by its api url. Beehive starts probing it at once, and buys postage batches on it only when batch creation is enabled.")
                .Produces(StatusCodes.Status201Created)
                .Produces(StatusCodes.Status400BadRequest);

            builder.MapPut("/nodes/{id}",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id,
                            [Required, FromBody] BeeNodeInput nodeInput) =>
                        handler.UpdateBeeNodeAsync(id, nodeInput))
                .WithDescription("Changes the api url of a registered node, or whether beehive may buy postage batches on it.")
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);
            
            builder.MapDelete("/nodes/{id}",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id) =>
                        handler.RemoveBeeNodeAsync(id))
                .WithDescription("Removes a bee node from beehive. The postage batches it owns stay in the cache, and can't be used until a node owning them is registered again.")
                .Produces(StatusCodes.Status200OK)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound);

            //nodes pins imports
            builder.MapGet("/nodes/pins/imports",
                    (IBeehiveApiHandler handler,
                            [Range(0, int.MaxValue)] int page = 0,
                            [Range(1, 10000)] int take = 100) =>
                        handler.GetNodesPinsImportsAsync(page, take))
                .WithDescription("Every import of the pinned contents of every node, newest first, with its counters: the running ones come first. An import whose background job ended without completing it is reported as failed.")
                .Produces<IEnumerable<NodePinsImportDto>>();

            builder.MapGet("/nodes/{id}/pins/imports",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id,
                            [Range(0, int.MaxValue)] int page = 0,
                            [Range(1, 10000)] int take = 100) =>
                        handler.GetNodePinsImportsAsync(id, page, take))
                .WithDescription("The imports of the pinned contents of a node, newest first, with their counters: the first one is the running or last import.")
                .Produces<IEnumerable<NodePinsImportDto>>();

            builder.MapPost("/nodes/{id}/pins/imports",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id,
                            [FromQuery] bool unpin = false,
                            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NodePinsImportInput? importInput = null) =>
                        handler.StartNodePinsImportAsync(id, unpin, importInput))
                .WithDescription("Starts, as a background job, the import into beehive's own pins of the contents pinned on the node: every one of them, or only the references given in the body. The chunks are read from the node itself. A content beehive already holds in full is skipped. With unpin, a content is unpinned on the node once beehive holds it in full, never before. One import at a time per node: 423 while one is running.")
                .Produces<NodePinsImportDto>(StatusCodes.Status202Accepted)
                .Produces(StatusCodes.Status400BadRequest)
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status423Locked);

            builder.MapGet("/nodes/{id}/pins/imports/{importId}/pins",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id,
                            [FromRoute] string importId,
                            [FromQuery] bool failedOnly = false,
                            [Range(0, int.MaxValue)] int page = 0,
                            [Range(1, 10000)] int take = 500) =>
                        handler.GetNodePinImportsAsync(id, importId, failedOnly, page, take))
                .WithDescription("One record per content of the import, newest first: its outcome (imported, already pinned, failed), the chunks pinned, missing or invalid, the failure reason, whether the import unpinned it on the node, and whether it is on the node right now (unknown when the node can't be asked). With failedOnly, the failed ones only.")
                .Produces<IEnumerable<NodePinImportDto>>();

            builder.MapDelete("/nodes/{id}/pins/imports",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id,
                            [FromQuery] bool? failed = null) =>
                        handler.DeleteNodePinsImportsAsync(id, failed))
                .WithDescription("Deletes the finished imports of a node, with their pin records: with failed=false the completed ones only, with failed=true the failed ones only, with no filter both. A running import is never deleted. Answers the number of deleted imports.")
                .Produces<long>();

            builder.MapGet("/nodes/{id}/pins/not-imported",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id) =>
                        handler.GetNodeNotImportedPinsAsync(id))
                .WithDescription("Asks the node for its pinned contents and answers the ones beehive doesn't hold in full yet: what an import of the node would read.")
                .Produces<IEnumerable<SwarmReference>>()
                .Produces(StatusCodes.Status404NotFound)
                .Produces(StatusCodes.Status503ServiceUnavailable);
        }
    }
}