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
using Etherna.Beehive.Extensions;
using Etherna.Beehive.HttpTransformers;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Hashing;
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Threading.Tasks;
using Yarp.ReverseProxy.Forwarder;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class FeedsControllerService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IFeedService feedService,
        IHttpForwarder forwarder,
        IDataService dataService)
        : IFeedsControllerService
    {
        public async Task<IActionResult> CreateFeedRootManifestAsync(
            EthAddress owner,
            string topic,
            SwarmFeedType type,
            PostageBatchId batchId,
            ushort compactLevel,
            bool pinContent)
        {
            var hashingResult = await dataService.UploadAsync(
                batchId,
                compactLevel > 0,
                pinContent,
                async (chunkStore, postageStamper) =>
                {
                    SwarmFeedBase swarmFeed = type switch
                    {
                        SwarmFeedType.Epoch => new SwarmEpochFeed(owner, topic.HexToByteArray(), new Hasher()),
                        SwarmFeedType.Sequence => new SwarmSequenceFeed(owner, topic.HexToByteArray()),
                        _ => throw new InvalidOperationException(),
                    };
                    
                    var result = await feedService.UploadFeedManifestAsync(swarmFeed, postageStamper, chunkStore);
                    return result.ChunkReference;
                });

            return new JsonResult(new ChunkReferenceDto(
                hashingResult.Hash,
                hashingResult.EncryptionKey,
                hashingResult.UseRecursiveEncryption))
            {
                StatusCode = StatusCodes.Status201Created
            };
        }

        public async Task<IResult> FindFeedUpdateAsync(
            EthAddress owner,
            string topic,
            HttpContext httpContext)
        {
            // Select node and forward request.
            var node = await beeNodeLiveManager.SelectHealthyNodeAsync();
            return await node.ForwardRequestAsync(
                forwarder,
                httpContext,
                new DownloadHttpTransformer(forceNoCache: true));
        }
    }
}