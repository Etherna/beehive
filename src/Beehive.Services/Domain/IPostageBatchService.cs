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

using Etherna.Beehive.Domain.Models;
using Etherna.BeeNet.Hashing.Postage;
using Etherna.BeeNet.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public interface IPostageBatchService
    {
        Task<ResourceLockHandler<PostageBatchLock>> AcquireLockAsync(
            PostageBatchId batchId,
            bool exclusiveAccess);

        Task<(PostageBatchId BatchId, EthTxHash TxHash)> BuyPostageBatchAsync(
            BzzBalance amount,
            int depth,
            string? label,
            bool immutable,
            ulong? gasLimit,
            XDaiBalance? gasPrice);
        
        Task<EthTxHash> DilutePostageBatchAsync(
            PostageBatchId batchId,
            int depth,
            ulong? gasLimit,
            XDaiBalance? gasPrice);
        
        Task<(PostageBatch PostageBatch, EthAddress Owner)[]> GetGlobalValidPostageBatchesAsync();
        
        Task<IEnumerable<PostageBatch>> GetOwnedPostageBatchesAsync();
        
        Task<bool> IsLockedAsync(PostageBatchId batchId);

        Task StoreStampedChunksAsync(
            PostageBatchCache postageBatchCache,
            HashSet<SwarmHash> stampedChunkHashesCache,
            IPostageStamper newPostageStamper);
        
        Task<EthTxHash> TopUpPostageBatchAsync(
            PostageBatchId batchId,
            BzzBalance amount,
            ulong? gasLimit,
            XDaiBalance? gasPrice);
        
        public Task<PostageBatchCache?> TryGetPostageBatchCacheAsync(
            PostageBatchId batchId,
            bool forceRefreshCache = false);

        Task<PostageBatch?> TryGetPostageBatchDetailsAsync(PostageBatchId batchId);
    }
}