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
using Etherna.Beehive.Services.Domain;
using Etherna.BeeNet.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Areas.Api.Bee.Services
{
    public class StampsControllerService(
        IPostageBatchService postageBatchService)
        : IStampsControllerService
    {
        // Methods.
        public async Task<IActionResult> BuyPostageBatchAsync(
            BzzBalance amount,
            int depth,
            string? label,
            bool immutable,
            ulong? gasLimit,
            XDaiBalance? gasPrice)
        {
            var (batchId, txHash) = await postageBatchService.BuyPostageBatchAsync(
                amount, depth, label, immutable, gasLimit, gasPrice);

            return new JsonResult(new PostageBatchIdWithTxHashDto(batchId, txHash))
            {
                StatusCode = StatusCodes.Status201Created
            };
        }

        public async Task<IActionResult> DilutePostageBatchAsync(
            PostageBatchId batchId,
            int depth,
            ulong? gasLimit,
            XDaiBalance? gasPrice)
        {
            var txHash = await postageBatchService.DilutePostageBatchAsync(batchId, depth, gasLimit, gasPrice);
            
            return new JsonResult(new PostageBatchIdWithTxHashDto(batchId, txHash))
            {
                StatusCode = StatusCodes.Status202Accepted
            };
        }

        public async Task<IActionResult> GetPostageBatchAsync(PostageBatchId batchId)
        {
            var postageBatch = await postageBatchService.TryGetPostageBatchDetailsAsync(batchId);
            if (postageBatch is null)
                return new NotFoundResult();
            return new JsonResult(new PostageBatchDto(
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
                postageBatch.Utilization));
        }

        public async Task<IActionResult> GetPostageBatchBucketsAsync(PostageBatchId batchId)
        {
            var postageBatch = await postageBatchService.TryGetPostageBatchCacheAsync(batchId);
            if (postageBatch is null)
                return new NotFoundResult();
            return new JsonResult(new PostageBatchBucketsDto(
                postageBatch.Depth,
                PostageBatch.BucketDepth,
                postageBatch.Buckets.Select((c, i) => new PostageBatchBucketDto(i, c))));
        }
    }
}