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

using Etherna.Beehive.Services.Utilities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Tasks
{
    public class PinContentInNodeTask(IBeeNodeLiveManager beeNodeLiveManager)
        : IPinContentInNodeTask
    {
        public const int MaxRetries = 5;
        public static readonly TimeSpan RetryWaitTime = TimeSpan.FromSeconds(5);
        
        public async Task RunAsync(string contentHash, string nodeId)
        {
            var beeNodeInstance = await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(nodeId);
            
            // Try to pin.
            for (int i = 0; i < MaxRetries; i++)
            {
                try
                {
                    await beeNodeInstance.PinResourceAsync(contentHash);
                }
                catch (KeyNotFoundException)
                {
                    if (i + 1 == MaxRetries)
                        throw;
                    await Task.Delay(RetryWaitTime);
                }
            }
        }
    }
}
