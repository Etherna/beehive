// Copyright 2021-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Etherna.BeehiveManager.Services.Utilities;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace Etherna.BeehiveManager.Extensions
{
    public static class ApplicationBuilderExtensions
    {
        public static void StartBeeNodeLiveManager(this IApplicationBuilder appBuilder)
        {
            if (appBuilder is null)
                throw new ArgumentNullException(nameof(appBuilder));

            var beeNodeLiveManager = appBuilder.ApplicationServices.GetRequiredService<IBeeNodeLiveManager>();

            var task = beeNodeLiveManager.LoadAllNodesAsync();
            task.Wait();

            beeNodeLiveManager.StartHealthHeartbeat();
        }
    }
}
