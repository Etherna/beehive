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
using Etherna.MongoDB.Driver;
using Etherna.MongODM.Core.Repositories;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Domain
{
    public sealed class ResourceLockService : IResourceLockService
    {
        public async Task<ResourceLockHandler<TModel>?> TryAcquireLockAsync<TModel>(
            Func<TModel> buildNewLock,
            IRepository<TModel, string> repository,
            string resourceId,
            bool exclusiveAccess)
            where TModel : ResourceLockBase
        {
            /*
             * Requirements:
             * - Locks on different resource Ids never collide, and concurrent acquisitions are always possible
             * - On same resourceId any number of locks are possible without exclusive access
             * - If any alive lock exists on a resourceId, new exclusive access on the same resourceId will fail
             * - If a lock with exclusive access exists on a resourceId, any other lock access will fail
             *
             * Solution:
             * - Index with unique restriction on resourceId is created
             * - An exclusive lock simply tries to create a new lock
             * - A not exclusive lock tries to exec atomic findAndUpdate with upsert, incrementing a counter.
             *   The release will decrement the counter, and if no locks remains, the lock is deleted.
             */
            ArgumentNullException.ThrowIfNull(buildNewLock, nameof(buildNewLock));
            ArgumentNullException.ThrowIfNull(repository, nameof(repository));

            var now = DateTime.UtcNow;

            // If prev lock exists, verify if is still valid, and if new one or old one are exclusive.
            var prevLock = await repository.TryFindOneAsync(l => l.ResourceId == resourceId);
            if (prevLock?.ExpirationTime > now && (exclusiveAccess || prevLock.ExclusiveAccess))
                return null;
            
            // Delete old expired lock, if exists.
            // Additional check on expiration because concurrent access could have renewed it.
            if (prevLock is not null)
            {
                try
                {
                    await repository.DeleteAsync(
                        prevLock,
                        [Builders<TModel>.Filter.Lte(l => l.ExpirationTime, now)]);
                }
                catch (MongoWriteException) { }
            }

            // Create or upsert new lock. It could still fail here because of concurrent accesses.
            // In that case unique index on resourceId permits only one to proceed.
            try
            {
                if (exclusiveAccess)
                {
                    await repository.CreateAsync(buildNewLock());
                }
                else
                {
                    await repository.UpsertAsync(
                        Builders<TModel>.Filter.And(
                            Builders<TModel>.Filter.Eq(l => l.ResourceId, resourceId),
                            Builders<TModel>.Filter.Eq(l => l.ExclusiveAccess,
                                false)), //don't update if ExclusiveAccess == true
                        Builders<TModel>.Update.Combine(
                            Builders<TModel>.Update.Inc(l => l.Counter, 1), //increment counter
                            Builders<TModel>.Update.Set(l => l.ExpirationTime, //renew expiration time
                                now.Add(ResourceLockBase.LockDuration))),
                        buildNewLock(),
                        [
                            nameof(ResourceLockBase.ExpirationTime),
                            nameof(ResourceLockBase.Counter)
                        ]);
                }

                return new ResourceLockHandler<TModel>(repository, this, resourceId, exclusiveAccess);
            }
            catch (Exception e) when (e is MongoCommandException
                                        or MongoWriteException)
            {
                // Catch any unique index collision.
                return null;
            }
        }

        public async Task<bool> IsLockedAsync<TModel>(
            IRepository<TModel, string> repository,
            string resourceId)
            where TModel : ResourceLockBase
        {
            ArgumentNullException.ThrowIfNull(repository, nameof(repository));
            
            var resourceLock = await repository.TryFindOneAsync(l => l.ResourceId == resourceId);
            return resourceLock != null && resourceLock.ExpirationTime > DateTime.UtcNow;
        }

        public async Task<bool> ReleaseLockAsync<TModel>(
            IRepository<TModel, string> repository,
            string resourceId,
            bool exclusiveAccess)
            where TModel : ResourceLockBase
        {
            ArgumentNullException.ThrowIfNull(repository, nameof(repository));
            
            // Find lock and optionally decrease counter.
            var resourceLock = exclusiveAccess ?
                
                //exclusive access
                await repository.TryFindOneAsync(
                    l => l.ResourceId == resourceId && l.ExclusiveAccess) :
                
                //not exclusive access
                await repository.FindOneAndUpdateAsync(
                    Builders<TModel>.Filter.And(
                        Builders<TModel>.Filter.Eq(l => l.ResourceId, resourceId),
                        Builders<TModel>.Filter.Eq(l => l.ExclusiveAccess, false),
                        Builders<TModel>.Filter.Gte(l => l.Counter, 1)),
                    Builders<TModel>.Update.Inc(l => l.Counter, -1),
                    new FindOneAndUpdateOptions<TModel>
                    {
                        ReturnDocument = ReturnDocument.After
                    });
            
            // If lock not found.
            if (resourceLock is null)
                return false;
            
            // Try delete document.
            if (resourceLock.Counter == 0)
            {
                List<FilterDefinition<TModel>> additionalFilters =
                    [Builders<TModel>.Filter.Eq(l => l.ExclusiveAccess, exclusiveAccess)];
                if (!exclusiveAccess)
                    additionalFilters.Add(Builders<TModel>.Filter.Eq(l => l.Counter, 0));
                
                await repository.DeleteAsync(
                    resourceLock,
                    additionalFilters.ToArray());
            }

            return true;
        }
    }
}