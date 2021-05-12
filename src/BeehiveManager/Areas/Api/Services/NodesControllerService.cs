using Etherna.BeehiveManager.Areas.Api.DtoModels;
using Etherna.BeehiveManager.Areas.Api.InputModels;
using Etherna.BeehiveManager.Domain;
using Etherna.BeehiveManager.Domain.Models;
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

            var node = new BeeNode(new Uri(input.Url), input.GatwayApiPort, input.DebugApiPort);
            await context.BeeNodes.CreateAsync(node);

            return new BeeNodeDto(node);
        }

        public async Task<IEnumerable<BeeNodeDto>> GetBeeNodesAsync(int page, int take) =>
            (await context.BeeNodes.QueryElementsAsync(elements =>
                elements.PaginateDescending(n => n.CreationDateTime, page, take)
                        .ToListAsync()))
            .Select(n => new BeeNodeDto(n));

        public async Task<BeeNodeDto> RefreshNodeInfoAsync(string id)
        {
            // Get client.
            var node = await context.BeeNodes.FindOneAsync(id);
            var nodeClient = beeNodesManager.GetBeeNodeClient(node);

            // Get info.
            //******TODO
            var ethAddress = "0x371f77a677E4D4CeB15D13DeF48fE4D2c45bf1D3";

            // Update node.
            node.SetInfoFromNodeInstance(ethAddress);

            // Save changes.
            await context.SaveChangesAsync();

            return new BeeNodeDto(node);
        }

        public async Task RemoveBeeNodeAsync(string id)
        {
            if (id is null)
                throw new ArgumentNullException(nameof(id));

            beeNodesManager.RemoveBeeNodeClient(id);
            await context.BeeNodes.DeleteAsync(id);
        }
    }
}
