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
using System.Text.RegularExpressions;
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

            // Normalize and verify url.
            var inputUrl = input.Url;
            if (inputUrl.Last() != '/')
                inputUrl += '/';

            //validate regex
            var urlRegex = new Regex(@"^((?<proto>\w+)://)?[^/]+?(?<port>:\d+)?/(?<path>.*)",
                RegexOptions.None, TimeSpan.FromMilliseconds(150));
            var urlMatch = urlRegex.Match(inputUrl);

            if (!urlMatch.Success)
                throw new ArgumentException("Url is not valid");

            if (!string.IsNullOrEmpty(urlMatch.Groups["path"].Value))
                throw new ArgumentException("Url can't have an internal path or query");

            if (!string.IsNullOrEmpty(urlMatch.Groups["port"].Value))
                throw new ArgumentException("Url can't specify a port");

            //add protocol
            if (string.IsNullOrEmpty(urlMatch.Groups["proto"].Value))
                inputUrl = $"{Uri.UriSchemeHttp}://{inputUrl}";

            // Create node.
            var node = new BeeNode(
                new Uri(inputUrl, UriKind.Absolute),
                input.GatwayApiPort,
                input.DebugApiPort);
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
