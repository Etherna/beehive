using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Areas.Api.InputModels;
using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
using Etherna.BeehiveManager.Domain.Models.BeeNodeAgg;
using Etherna.BeehiveManager.Services.Utilities;
using Etherna.MongODM.Core.Extensions;
using MongoDB.Driver;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Areas.Api.Services
{
    public class NodesControllerService : INodesControllerService
    {
        // Fields.
        private readonly IBeeNodesManager beeNodesManager;
        private readonly IBeehiveContext context;

        // Constructor.
        public NodesControllerService(
            IBeeNodesManager beeNodesManager,
            IBeehiveContext context)
        {
            this.beeNodesManager = beeNodesManager;
            this.context = context;
        }

        // Methods.
        public async Task<BeeNodeDto> AddBeeNodeAsync(BeeNodeInput input)
        {
            if (input is null)
                throw new ArgumentNullException(nameof(input));

            // Create node.
            var node = new BeeNode(
                input.DebugApiPort,
                input.GatwayApiPort,
                input.Url);
            await context.BeeNodes.CreateAsync(node);

            return new BeeNodeDto(node);
        }

        public async Task<BeeNodeDto> FindByIdAsync(string id) =>
            new BeeNodeDto(await context.BeeNodes.FindOneAsync(id));

        public async Task<IEnumerable<BeeNodeDto>> GetBeeNodesAsync(int page, int take) =>
            (await context.BeeNodes.QueryElementsAsync(elements =>
                elements.PaginateDescending(n => n.CreationDateTime, page, take)
                        .ToListAsync()))
            .Select(n => new BeeNodeDto(n));

        public async Task RemoveBeeNodeAsync(string id)
        {
            if (id is null)
                throw new ArgumentNullException(nameof(id));

            beeNodesManager.RemoveBeeNodeClient(id);
            await context.BeeNodes.DeleteAsync(id);
        }

        public async Task<BeeNodeDto> RetrieveAddressesAsync(string id)
        {
            // Get client.
            var node = await context.BeeNodes.FindOneAsync(id);
            var nodeClient = beeNodesManager.GetBeeNodeClient(node);

            // Get info.
            if (nodeClient.DebugClient is null)
                throw new InvalidOperationException("Node is not configured for debug api");
            var response = await nodeClient.DebugClient.AddressesAsync();

            // Update node.
            node.SetAddresses(new BeeNodeAddresses(
                response.Ethereum,
                response.Overlay,
                response.Pss_public_key,
                response.Public_key));

            // Save changes.
            await context.SaveChangesAsync();

            return new BeeNodeDto(node);
        }
    }
}
