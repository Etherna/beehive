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
using Etherna.Beehive.Configs;
using Etherna.Beehive.Domain;
using Etherna.Beehive.Extensions;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Utilities;
using Etherna.BeeNet.Chunks;
using Etherna.BeeNet.Hashing;
using Etherna.BeeNet.Models;
using Etherna.BeeNet.Services;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class FeedsControllerService(
        IBeeNodeLiveManager beeNodeLiveManager,
        IDataService dataService,
        IBeehiveDbContext dbContext,
        IFeedService feedService)
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
                null,
                compactLevel > 0,
                pinContent,
                async (chunkStore, postageStamper) =>
                {
                    SwarmFeedBase swarmFeed = type switch
                    {
                        SwarmFeedType.Epoch => new SwarmEpochFeed(owner, topic.HexToByteArray()),
                        SwarmFeedType.Sequence => new SwarmSequenceFeed(owner, topic.HexToByteArray()),
                        _ => throw new InvalidOperationException(),
                    };
                    
                    return await feedService.UploadFeedManifestAsync(
                        swarmFeed,
                        () => new Hasher(),
                        compactLevel,
                        postageStamper,
                        chunkStore);
                });

            return new JsonResult(new SimpleChunkReferenceDto(hashingResult.Hash))
            {
                StatusCode = StatusCodes.Status201Created
            };
        }

        public async Task<IActionResult> FindFeedUpdateAsync(
            EthAddress owner,
            string topic,
            long? at,
            ulong? after,
            byte? afterLevel,
            SwarmFeedType type,
            bool onlyRootChunk,
            bool resolveLegacyPayload,
            HttpResponse response)
        {
            ArgumentNullException.ThrowIfNull(response, nameof(response));
            
            // Init.
            at ??= DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var topicByteArray = topic.HexToByteArray();
            
            // Build feed.
            SwarmFeedBase feed = type switch
            {
                SwarmFeedType.Epoch => new SwarmEpochFeed(owner, topicByteArray),
                SwarmFeedType.Sequence => new SwarmSequenceFeed(owner, topicByteArray),
                _ => throw new InvalidOperationException()
            };
            SwarmFeedIndexBase? afterFeedIndex = after is null
                ? null
                : type switch
                {
                    SwarmFeedType.Epoch => new SwarmEpochFeedIndex(after.Value, afterLevel ?? SwarmEpochFeedIndex.MaxLevel, new Hasher()),
                    SwarmFeedType.Sequence => new SwarmSequenceFeedIndex(after.Value),
                    _ => throw new InvalidOperationException(),
                };

            // Find feed chunk at given moment.
            await using var chunkStore = new BeehiveChunkStore(beeNodeLiveManager, dbContext);
            var feedChunk = await feed.TryFindFeedAtAsync(chunkStore, at.Value, afterFeedIndex, () => new Hasher());
            if (feedChunk is null)
                throw new KeyNotFoundException("No feed update found");
            
            // Keep compatibility with Bee, and return next feed index if it's a sequence feed.
            // Otherwise, it wouldn't be required because returning the current sequence index
            // to find the next is trivial (prev+1). Instead, in case of an epoch feed, we would need
            // an actual "at" value to compose it. In both cases better to do client side.
            var nextFeedIndex = type == SwarmFeedType.Sequence ? feedChunk.Index.GetNext(0) : null;

            // Unwrap original chunk from feed chunk.
            var (unwrappedChunk, soc) = await feedChunk.UnwrapChunkAndSocAsync(resolveLegacyPayload, new Hasher(), chunkStore);
            
            // Build response headers.
            var currentIndexBytes = feedChunk.Index.MarshalBinary();
            var nextIndexBytes = nextFeedIndex?.MarshalBinary();
            var signature = soc.Signature!.Value.ToArray();

            response.Headers.Append(SwarmHttpConsts.SwarmFeedIndexHeader, currentIndexBytes.ToHex());
            if (nextIndexBytes != null)
                response.Headers.Append(SwarmHttpConsts.SwarmFeedIndexNextHeader, nextIndexBytes.ToHex());
            response.Headers.Append(SwarmHttpConsts.SwarmSocSignatureHeader, signature.ToHex());
            response.Headers.Append(CorsConstants.AccessControlExposeHeaders, new StringValues(
                [
                    SwarmHttpConsts.SwarmFeedIndexHeader,
                    SwarmHttpConsts.SwarmFeedIndexNextHeader,
                    SwarmHttpConsts.SwarmSocSignatureHeader
                ]));
            response.Headers.SetNoCache(); //disable cache

            // Return content.
            //if only root, returns chunk's data
            if (onlyRootChunk)
                return new FileContentResult(
                    unwrappedChunk.Data.ToArray(),
                    BeehiveHttpConsts.OctetStreamContentType);

            //else return joined data
            var chunkJoiner = new ChunkJoiner(chunkStore);
            var dataStream = await chunkJoiner.GetJoinedChunkDataAsync(unwrappedChunk, null, false);

            return new FileStreamResult(
                dataStream,
                BeehiveHttpConsts.OctetStreamContentType);
        }
    }
}