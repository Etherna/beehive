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
using Etherna.Beehive.Domain.Models;
using Etherna.SwarmSdk.Models;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading;

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
            //deprecated chunks
            builder.MapGet("/deprecated-chunks",
                    (IBeehiveApiHandler handler, CancellationToken cancellationToken) =>
                        handler.CountDeprecatedChunksAsync(cancellationToken))
                .WithDescription("The chunks still in the deprecated stores, as estimated by the db: the documents of the chunks collection, and the files of the GridFS bucket. Both at zero when every chunk was migrated.")
                .Produces<DeprecatedChunksDto>();

            builder.MapGet("/deprecated-chunks/migrations",
                    (IBeehiveApiHandler handler,
                            CancellationToken cancellationToken,
                            [Range(0, int.MaxValue)] int page = 0,
                            [Range(1, 10000)] int take = 100) =>
                        handler.GetChunksMigrationsAsync(page, take, cancellationToken))
                .WithDescription("Every migration of the deprecated chunks, newest first, with its counters per store: the first one is the running or last migration. A migration whose background job ended without completing it is reported as failed.")
                .Produces<IEnumerable<ChunksMigrationDto>>();

            builder.MapPost("/deprecated-chunks/migrations",
                    (IBeehiveApiHandler handler, CancellationToken cancellationToken) =>
                        handler.StartChunksMigrationAsync(cancellationToken))
                .WithDescription("Starts, as a background job, the migration of the deprecated chunks to the chunks keyed by hash: first the documents of the chunks collection, with their pins, then the files of the GridFS bucket. A chunk held already takes the pins of the migrated one. Every migrated chunk is deleted from its deprecated store, so a new migration goes on with what is left. A chunk that can't be migrated is counted as failed and left in its store. One migration at a time: 423 while one is running.")
                .Produces<ChunksMigrationDto>(StatusCodes.Status202Accepted)
                .Produces<string>(StatusCodes.Status423Locked);

            builder.MapPost("/deprecated-chunks/migrations/{id}/stop",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id,
                            CancellationToken cancellationToken) =>
                        handler.StopChunksMigrationAsync(id, cancellationToken))
                .WithDescription("Asks a migration to stop: its job stops at the end of the page of chunks it is migrating, and the migration ends as stopped. A migration already ended is answered as it is.")
                .Produces<ChunksMigrationDto>()
                .Produces<string>(StatusCodes.Status404NotFound);

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
                .Produces<string>(StatusCodes.Status400BadRequest)
                .Produces<string>(StatusCodes.Status404NotFound);

            builder.MapPost("/nodes",
                    (IBeehiveApiHandler handler,
                            [Required, FromBody] BeeNodeInput nodeInput) =>
                        handler.AddBeeNodeAsync(nodeInput))
                .WithDescription("Registers a bee node by its api url. Beehive starts probing it at once, and buys postage batches on it only when batch creation is enabled.")
                .Produces(StatusCodes.Status201Created)
                .Produces<string>(StatusCodes.Status400BadRequest);

            builder.MapPut("/nodes/{id}",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id,
                            [Required, FromBody] BeeNodeInput nodeInput) =>
                        handler.UpdateBeeNodeAsync(id, nodeInput))
                .WithDescription("Changes the api url of a registered node, or whether beehive may buy postage batches on it.")
                .Produces(StatusCodes.Status200OK)
                .Produces<string>(StatusCodes.Status400BadRequest)
                .Produces<string>(StatusCodes.Status404NotFound);
            
            builder.MapDelete("/nodes/{id}",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id) =>
                        handler.RemoveBeeNodeAsync(id))
                .WithDescription("Removes a bee node from beehive. The postage batches it owns stay in the cache, bound to the id of the removed node: they can't be used anymore, not even after registering the same node again, which gets a new id.")
                .Produces(StatusCodes.Status200OK)
                .Produces<string>(StatusCodes.Status400BadRequest)
                .Produces<string>(StatusCodes.Status404NotFound);

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
                .WithDescription("The imports of the pinned contents of a node, newest first, with their counters: the first one is the running or last import. An import whose background job ended without completing it is reported as failed.")
                .Produces<IEnumerable<NodePinsImportDto>>();

            builder.MapPost("/nodes/{id}/pins/imports",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id,
                            [FromQuery] bool unpin = false,
                            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] NodePinsImportInput? importInput = null) =>
                        handler.StartNodePinsImportAsync(id, unpin, importInput))
                .WithDescription("Starts, as a background job, the import into beehive's own pins of the contents pinned on the node: every one of them, or only the references given in the body. The chunks are read from the node itself. A content beehive already holds in full is skipped. With unpin, a content is unpinned on the node once beehive holds it in full, never before. One import at a time per node: 423 while one is running.")
                .Produces<NodePinsImportDto>(StatusCodes.Status202Accepted)
                .Produces<string>(StatusCodes.Status400BadRequest)
                .Produces<string>(StatusCodes.Status404NotFound)
                .Produces<string>(StatusCodes.Status409Conflict)
                .Produces<string>(StatusCodes.Status423Locked);

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
                .Produces<string>(StatusCodes.Status404NotFound)
                .Produces<string>(StatusCodes.Status503ServiceUnavailable);

            //runs of the pins check
            builder.MapGet("/pins/check/runs",
                    (IBeehiveApiHandler handler,
                            CancellationToken cancellationToken,
                            [Range(0, int.MaxValue)] int page = 0,
                            [Range(1, 10000)] int take = 100) =>
                        handler.GetPinsChecksAsync(page, take, cancellationToken))
                .WithDescription("Every background run of the pins check, newest first, with its pins by outcome: the first one is the running or last run. A run whose background job ended without completing it is reported as failed.")
                .Produces<IEnumerable<PinsCheckDto>>();

            builder.MapGet("/pins/check/runs/{id}",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id,
                            CancellationToken cancellationToken) =>
                        handler.GetPinsCheckAsync(id, cancellationToken))
                .WithDescription("A background run of the pins check, by id: what it checks, the content it is on, its pins by outcome against the total, its job and its times.")
                .Produces<PinsCheckDto>()
                .Produces<string>(StatusCodes.Status404NotFound);

            builder.MapPost("/pins/check/runs",
                    (IBeehiveApiHandler handler,
                            CancellationToken cancellationToken,
                            [FromQuery] ChunkPinState[]? state = null,
                            [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] PinsCheckInput? checkInput = null) =>
                        handler.StartPinsCheckAsync(state, checkInput, cancellationToken))
                .WithDescription("Starts a background run of the pins check: the check GET /pins/check answers as a stream, run as a job that records the outcome of every pinned content, since a check of a whole cluster takes hours. It takes every pin, the pins of the given states, or the references given in the body only; a state and a body together are refused. A pin is in one state: succeeded (processed, with every chunk of its content), incomplete (processed, with chunks beehive doesn't hold) or unprocessed (its pinning didn't run, or didn't end); state is given once per state to take, so state=incomplete&state=unprocessed takes every pin not succeeded. Every pin is brought to what its check finds: one found whole is declared succeeded when it was not; one found incomplete goes back to not succeeded with its missing chunks, and its invalid chunks are deleted. Refused with 409 while a deprecated chunk store still holds chunks. One check of many pins at a time: 423 while a run is running, or while GET /pins/check is answering a check of every pin.")
                .Produces<PinsCheckDto>(StatusCodes.Status202Accepted)
                .Produces<string>(StatusCodes.Status400BadRequest)
                .Produces<string>(StatusCodes.Status409Conflict)
                .Produces<string>(StatusCodes.Status423Locked);

            builder.MapGet("/pins/check/runs/{id}/pins",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id,
                            CancellationToken cancellationToken,
                            [FromQuery] PinCheckOutcome? outcome = null,
                            [Range(0, int.MaxValue)] int page = 0,
                            [Range(1, 10000)] int take = 500) =>
                        handler.GetPinsCheckPinsAsync(id, outcome, page, take, cancellationToken))
                .WithDescription("One record per pinned content of the run, newest first: its outcome (complete, incomplete, invalid, failed), the chunks beehive doesn't hold, the invalid ones and every chunk of the content, as the check of a pin counts them, and why it could not be checked. With outcome, the records of that outcome only.")
                .Produces<IEnumerable<PinCheckDto>>()
                .Produces<string>(StatusCodes.Status400BadRequest);

            builder.MapPost("/pins/check/runs/{id}/stop",
                    (IBeehiveApiHandler handler,
                            [FromRoute] string id,
                            CancellationToken cancellationToken) =>
                        handler.StopPinsCheckAsync(id, cancellationToken))
                .WithDescription("Asks a run to stop: its job stops at the end of the pin it is checking, and the run ends as stopped. The pins it checked keep their correction. A run already ended is answered as it is.")
                .Produces<PinsCheckDto>()
                .Produces<string>(StatusCodes.Status404NotFound);

            builder.MapDelete("/pins/check/runs",
                    (IBeehiveApiHandler handler,
                            CancellationToken cancellationToken,
                            [FromQuery] bool? failed = null) =>
                        handler.DeletePinsChecksAsync(failed, cancellationToken))
                .WithDescription("Deletes the finished runs, with their pin records: with failed=true the failed ones only, with failed=false the completed and the stopped ones only, with no filter both. A running one is never deleted. Answers the number of deleted runs.")
                .Produces<long>();
        }
    }
}