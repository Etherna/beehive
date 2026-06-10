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
using Etherna.Beehive.Configs;
using Etherna.Beehive.Domain;
using Etherna.Beehive.Services.Domain;
using Etherna.SwarmSdk.Models;
using Microsoft.AspNetCore.Http;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.SwarmApiHandlers
{
    public sealed class StampsApiHandler(
        IBeehiveDbContext dbContext,
        IPostageBatchService postageBatchService)
        : IStampsApiHandler
    {
        public Task<IResult> BuyPostageBatchAsync(
            BzzValue amount,
            int depth,
            string? label,
            bool immutable,
            ulong? gasLimit,
            XDaiValue? gasPrice) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                var (batchId, txHash) = await postageBatchService.BuyPostageBatchAsync(
                    amount, depth, label, immutable, gasLimit, gasPrice);

                return Results.Json(
                    new PostageBatchIdWithTxHashDto(batchId, txHash),
                    CommonConsts.SwarmJsonSerializerOptions,
                    statusCode: StatusCodes.Status201Created);
            });

        public Task<IResult> DilutePostageBatchAsync(
            PostageBatchId batchId,
            int depth,
            ulong? gasLimit,
            XDaiValue? gasPrice) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                var txHash = await postageBatchService.DilutePostageBatchAsync(batchId, depth, gasLimit, gasPrice);
            
                return Results.Json(
                    new PostageBatchIdWithTxHashDto(batchId, txHash),
                    CommonConsts.SwarmJsonSerializerOptions,
                    statusCode: StatusCodes.Status202Accepted);
            });

        public Task<IResult> GetOwnedPostageBatchesAsync() =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                var postageBatches = await postageBatchService.GetOwnedPostageBatchesAsync();

                return Results.Json(
                    new PostageBatchStampListDto(postageBatches.Select(b =>
                        new PostageBatchDto(
                            b.Amount,
                            b.Id,
                            b.Ttl,
                            b.BlockNumber,
                            PostageBatch.BucketDepth,
                            b.Depth,
                            b.Exists,
                            b.IsImmutable,
                            b.Label,
                            b.IsUsable,
                            b.Utilization))),
                    CommonConsts.SwarmJsonSerializerOptions);
            });

        public Task<IResult> GetPostageBatchAsync(PostageBatchId batchId) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                var postageBatch = await postageBatchService.TryGetPostageBatchDetailsAsync(batchId);
                if (postageBatch is null)
                    throw new KeyNotFoundException();
                return Results.Json(
                    new PostageBatchDto(
                        postageBatch.Amount,
                        postageBatch.Id,
                        postageBatch.Ttl,
                        postageBatch.BlockNumber,
                        PostageBatch.BucketDepth,
                        postageBatch.Depth,
                        postageBatch.Exists,
                        postageBatch.IsImmutable,
                        postageBatch.Label,
                        postageBatch.IsUsable,
                        postageBatch.Utilization),
                    CommonConsts.SwarmJsonSerializerOptions);
            });

        public Task<IResult> GetPostageBatchBucketsAsync(PostageBatchId batchId) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                var postageBatch = await dbContext.PostageBatchesCache.TryFindOneAsync(b => b.BatchId == batchId);
                if (postageBatch is null)
                    throw new KeyNotFoundException();// return new BeeNotFoundResult();
                return Results.Json(
                    new PostageBatchBucketsDto(
                        postageBatch.Depth,
                        PostageBatch.BucketDepth,
                        postageBatch.Buckets.Select((c, i) => new PostageBatchBucketDto(i, c))),
                    CommonConsts.SwarmJsonSerializerOptions);
            });

        public Task<IResult> TopUpPostageBatchAsync(
            PostageBatchId batchId,
            BzzValue amount,
            ulong? gasLimit,
            XDaiValue? gasPrice) =>
            ExceptionHandler.RunAsync(ApiVersion.Swarm, async () =>
            {
                var txHash = await postageBatchService.TopUpPostageBatchAsync(batchId, amount, gasLimit, gasPrice);
            
                return Results.Json(
                    new PostageBatchIdWithTxHashDto(batchId, txHash),
                    CommonConsts.SwarmJsonSerializerOptions,
                    statusCode: StatusCodes.Status202Accepted);
            });
    }
}