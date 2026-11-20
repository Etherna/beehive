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

using Etherna.Beehive.Helpers;
using Etherna.Beehive.Services.Domain;
using Etherna.Scrinium.Core.Serialization.Modifiers;
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Areas.Api.SwarmApiHandlers
{
    public class SocApiHandlerTest
    {
        // Consts.
        private const string OwnerPrivateKey = "0x4f3edf983ac636a65a842ce7c78d9aa706d3b113bce9c46f30d7d21715b23b1d";

        // Tests.

        [Fact]
        public async Task ResolveServesTheWrappedContentReadFromTheNodesWhileTheResponseIsWritten()
        {
            // Setup.
            //beehive holds a single owner chunk wrapping the root of a 600 KiB content and none of the other chunks;
            //a healthy node holds them all
            var nodeChunks = new MemoryChunkStore();
            var (reference, content) = await HandlerTestEnvironment.NewContentAsync(600 * 1024, nodeChunks);
            var signer = new PrivateKeySigner(new EthPrivateKey(OwnerPrivateKey));
            SwarmSocIdentifier id = Enumerable.Range(0, SwarmSocIdentifier.IdentifierSize).Select(i => (byte)i).ToArray();
            var soc = new SwarmSoc(id, signer.PublicAddress, (SwarmCac)nodeChunks.AllChunks[reference.Hash]);
            soc.Sign(signer, new Hasher());
            var env = new HandlerTestEnvironment(HandlerTestEnvironment.NewAliveNode(nodeChunks));
            env.DbChunks.Add(HandlerTestEnvironment.DocumentOf(soc));
            var handler = new SocApiHandler(
                env.BeeNodeLiveManager,
                new Mock<IDataService>().Object,
                env.DbContext,
                env.HttpContextAccessor,
                NullLogger<SocApiHandler>.Instance,
                new Mock<ISerializerModifierAccessor>().Object);

            // Action.
            //the handler returns the file result, then the host writes the body: the chunks are read from the node now
            var result = await handler.ResolveSocAsync(signer.PublicAddress, id, false, RedundancyStrategy.None, false);
            await result.ExecuteAsync(env.HttpContext);

            // Assert.
            Assert.Equal(StatusCodes.Status200OK, env.HttpContext.Response.StatusCode);
            Assert.Equal(content, env.GetResponseBody());
            Assert.All( //every chunk read from the node was saved; the root is wrapped in the soc, never read
                nodeChunks.AllChunks.Keys.Where(h => h != reference.Hash),
                h => Assert.Contains(env.DbChunks, d => d.Id == h));
        }
    }
}
