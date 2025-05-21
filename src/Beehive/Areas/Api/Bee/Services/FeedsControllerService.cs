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
using Etherna.MongODM.Core.Serialization.Modifiers;
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
        IFeedService feedService,
        ISerializerModifierAccessor serializerModifierAccessor)
        : IFeedsControllerService
    {
        public async Task<IActionResult> CreateFeedRootManifestAsync(
            EthAddress owner,
            SwarmFeedTopic topic,
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
                        SwarmFeedType.Epoch => new SwarmEpochFeed(owner, topic),
                        SwarmFeedType.Sequence => new SwarmSequenceFeed(owner, topic),
                        _ => throw new InvalidOperationException(),
                    };
                    
                    return await feedService.UploadFeedManifestAsync(
                        swarmFeed,
                        new Hasher(),
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
            SwarmFeedTopic topic,
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
            
            // Build feed.
            SwarmFeedBase feed = type switch
            {
                SwarmFeedType.Epoch => new SwarmEpochFeed(owner, topic),
                SwarmFeedType.Sequence => new SwarmSequenceFeed(owner, topic),
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
            await using var chunkStore = new BeehiveChunkStore(beeNodeLiveManager, dbContext, serializerModifierAccessor);
            var feedChunk = await feed.TryFindFeedChunkAtAsync(
                at.Value,
                afterFeedIndex,
                chunkStore,
                new Hasher());
            if (feedChunk is null)
                throw new KeyNotFoundException("No feed update found");
            
            // Keep compatibility with Bee, and return next feed index if it's a sequence feed.
            // Otherwise, it wouldn't be required because returning the current sequence index
            // to find the next is trivial (prev+1). Instead, in case of an epoch feed, we would need
            // an actual "at" value to compose it. In both cases better to do client side.
            var nextFeedIndex = type == SwarmFeedType.Sequence ? feedChunk.Index.GetNext(0) : null;

            // Unwrap original chunk from feed chunk.
            var wrappedChunk = await feedChunk.UnwrapDataChunkAsync(
                resolveLegacyPayload,
                new SwarmChunkBmt(),
                chunkStore);
            
            // Build response headers.
            var currentIndexBytes = feedChunk.Index.MarshalBinary();
            var nextIndexBytes = nextFeedIndex?.MarshalBinary();
            var signature = feedChunk.Signature!.Value;

            response.Headers.Append(SwarmHttpConsts.SwarmFeedIndexHeader, currentIndexBytes.ToHex());
            if (nextIndexBytes != null)
                response.Headers.Append(SwarmHttpConsts.SwarmFeedIndexNextHeader, nextIndexBytes.ToHex());
            response.Headers.Append(SwarmHttpConsts.SwarmSocSignatureHeader, signature.ToString());
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
                    wrappedChunk.Data.ToArray(),
                    BeehiveHttpConsts.ApplicationOctetStreamContentType);

            //else return joined data
            var dataStream = ChunkDataStream.BuildNew(wrappedChunk, null, false, chunkStore);
            return new FileStreamResult(
                dataStream,
                BeehiveHttpConsts.ApplicationOctetStreamContentType);
        }
    }
}