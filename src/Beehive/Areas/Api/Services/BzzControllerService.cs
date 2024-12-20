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

using Etherna.Beehive.Extensions;
using Etherna.Beehive.Services.Utilities;
using Etherna.Beehive.Tools;
using Etherna.BeeNet.Manifest;
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Stores;
using Microsoft.AspNetCore.Http;
using System;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace Etherna.Beehive.Areas.Api.Services
{
    public class BzzControllerService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IDbChunkStore chunkStore,
        IHttpForwarder forwarder)
        : IBzzControllerService
    {
        [SuppressMessage("Design", "CA1031:Do not catch general exception types")]
        [SuppressMessage("ReSharper", "EmptyGeneralCatchClause")]
        public async Task<IResult> DownloadBzzAsync(
            SwarmAddress address,
            HttpContext httpContext)
        {
            // Try to get from chunk's db.
            try
            {
                var chunkJoiner = new ChunkJoiner(chunkStore);
                var rootManifest = new ReferencedMantarayManifest(
                    chunkStore,
                    address.Hash);

                var chunkReference = await rootManifest.ResolveAddressToChunkReferenceAsync(address.Path)
                    .ConfigureAwait(false);

                var metadata = await rootManifest.GetResourceMetadataAsync(address);
                var dataStream = await chunkJoiner.GetJoinedChunkDataAsync(
                    chunkReference,
                    null,
                    CancellationToken.None).ConfigureAwait(false);

                metadata.TryGetValue("Content-Type", out var contentType);
                metadata.TryGetValue("Filename", out var fileName);

                return Results.File(dataStream, contentType, fileName);
            }
            catch
            {
            } //proceed with forward on any error

            // Select node and forward request.
            var node = beeNodeLiveManager.SelectDownloadNode(address);
            return await node.ForwardRequestAsync(forwarder, httpContext);
        }

        public async Task<IResult> UploadBzzAsync(HttpContext httpContext)
        {
            // Get postage batch Id.
            var batchId = httpContext.TryGetPostageBatchId();
            if (batchId is null)
                throw new InvalidOperationException();
            
            // Select node and forward request.
            var node = beeNodeLiveManager.GetBeeNodeLiveInstanceByOwnedPostageBatch(batchId.Value);
            return await node.ForwardRequestAsync(forwarder, httpContext);
        }
    }
}