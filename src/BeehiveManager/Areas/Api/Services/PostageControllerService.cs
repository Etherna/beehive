﻿using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.BeehiveManager.Services.Utilities.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class PostageControllerService : IPostageControllerService
    {
        // Fields.
        private readonly IBeehiveDbContext beehiveDbContext;
        private readonly IBeeNodeLiveManager beeNodeLiveManager;

        // Constructor.
        public PostageControllerService(
            IBeehiveDbContext beehiveDbContext,
            IBeeNodeLiveManager beeNodeLiveManager)
        {
            this.beehiveDbContext = beehiveDbContext;
            this.beeNodeLiveManager = beeNodeLiveManager;
        }

        // Methods.
        public async Task<PostageBatchRefDto> BuyPostageBatchAsync(
            long amount, int depth, long? gasPrice, bool immutable, string? label, string? nodeId)
        {
            // Try to select an healthy node.
            var beeNodeInstance = nodeId is null ?
                beeNodeLiveManager.TrySelectHealthyNodeAsync(BeeNodeSelectionMode.RoundRobin) :
                await beeNodeLiveManager.GetBeeNodeLiveInstanceAsync(nodeId);

            if (beeNodeInstance is null)
                throw new InvalidOperationException("No healthy nodes available");

            // Buy postage.
            var batchId = await beeNodeInstance.BuyPostageBatchAsync(amount, depth, label, immutable, gasPrice);

            return new PostageBatchRefDto(batchId, beeNodeInstance.Id);
        }

        public async Task<BeeNodeDto> FindBeeNodeOwnerOfPostageBatchAsync(string batchId)
        {
            var beeNodeLiveInstance = beeNodeLiveManager.AllNodes.FirstOrDefault(n => n.Status.PostageBatchesId?.Contains(batchId) ?? false);
            if (beeNodeLiveInstance is null)
                throw new KeyNotFoundException();

            var beeNode = await beehiveDbContext.BeeNodes.FindOneAsync(beeNodeLiveInstance.Id);

            return new BeeNodeDto(beeNode);
        }
    }
}
