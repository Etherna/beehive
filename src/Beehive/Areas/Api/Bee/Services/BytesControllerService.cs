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
using Etherna.Beehive.Services.Chunks;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Chunks;
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class BytesControllerService(
        IBeehiveChunkStore beehiveChunkStore,
        IBeeNodeLiveManager beeNodeLiveManager,
        IHttpForwarder forwarder)
        : IBytesControllerService
    {
        public async Task<IActionResult> DownloadBytesAsync(
            SwarmHash hash,
            HttpContext httpContext)
        {
            var chunkJoiner = new ChunkJoiner(beehiveChunkStore);
            var dataStream = await chunkJoiner.GetJoinedChunkDataAsync(new SwarmChunkReference(hash, null, false));

            return new FileStreamResult(dataStream, "application/octet-stream");
        }

        public async Task<IResult> UploadBytesAsync(
            PostageBatchId batchId,
            HttpContext httpContext)
        {
            // Select node and forward request.
            var node = beeNodeLiveManager.SelectUploadNode(batchId);
            return await node.ForwardRequestAsync(forwarder, httpContext);
        }
    }
}