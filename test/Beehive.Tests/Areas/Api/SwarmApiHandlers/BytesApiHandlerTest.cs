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
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Areas.Api.SwarmApiHandlers
{
    public class BytesApiHandlerTest
    {
        // Tests.

        [Fact]
        public async Task DownloadAnswersNotFoundWhenAChunkIsMissingOnTheFirstRead()
        {
            // Setup.
            //beehive holds the root chunk of a content and none of its data chunks, and no healthy node can be asked
            var chunks = new MemoryChunkStore();
            var (reference, _) = await HandlerTestEnvironment.NewContentAsync(10 * 1024, chunks);
            var dataChunkHashes = HandlerTestEnvironment.ChildHashesOf(reference, chunks);
            var env = new HandlerTestEnvironment();
            env.DbChunks.Add(HandlerTestEnvironment.DocumentOf(chunks.AllChunks[reference.Hash]));
            var handler = NewHandler(env);

            // Action.
            await env.ExecuteSwarmApiAsync(() => handler.DownloadBytesAsync(reference, RedundancyLevel.None, RedundancyStrategy.None, false));

            // Assert.
            //the first read of the body fails before any byte is written: answered, naming the root chunk and the child it couldn't fetch
            Assert.Equal(StatusCodes.Status404NotFound, env.HttpContext.Response.StatusCode);
            var message = ReadErrorMessage(env);
            Assert.Contains(reference.Hash.ToString(), message, StringComparison.Ordinal);
            Assert.Contains(dataChunkHashes[0].ToString(), message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task DownloadOfARangeAnswersNotFoundWhenItsFirstChunkIsMissing()
        {
            // Setup.
            //beehive holds the root chunk and the first two data chunks of a 10 KiB content, not the third, and no healthy node can be asked
            var chunks = new MemoryChunkStore();
            var (reference, _) = await HandlerTestEnvironment.NewContentAsync(10 * 1024, chunks);
            var dataChunkHashes = HandlerTestEnvironment.ChildHashesOf(reference, chunks);
            var env = new HandlerTestEnvironment();
            foreach (var hash in new[] { reference.Hash, dataChunkHashes[0], dataChunkHashes[1] })
                env.DbChunks.Add(HandlerTestEnvironment.DocumentOf(chunks.AllChunks[hash]));
            env.HttpContext.Request.Headers.Range = "bytes=9000-"; //starts inside the third chunk
            var handler = NewHandler(env);

            // Action.
            await env.ExecuteSwarmApiAsync(() => handler.DownloadBytesAsync(reference, RedundancyLevel.None, RedundancyStrategy.None, false));

            // Assert.
            //the first read of the range fails before any byte is written: answered, naming the root chunk and the missing child only
            Assert.Equal(StatusCodes.Status404NotFound, env.HttpContext.Response.StatusCode);
            var message = ReadErrorMessage(env);
            Assert.Contains(reference.Hash.ToString(), message, StringComparison.Ordinal);
            Assert.Contains(dataChunkHashes[2].ToString(), message, StringComparison.Ordinal);
            Assert.DoesNotContain(dataChunkHashes[0].ToString(), message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task DownloadServesTheChunksReadFromTheNodesWhileTheResponseIsWritten()
        {
            // Setup.
            //beehive holds the root chunk of a 600 KiB content and none of its other chunks; a healthy node holds them all
            var nodeChunks = new MemoryChunkStore();
            var (reference, content) = await HandlerTestEnvironment.NewContentAsync(600 * 1024, nodeChunks);
            var env = new HandlerTestEnvironment(HandlerTestEnvironment.NewAliveNode(nodeChunks));
            env.DbChunks.Add(HandlerTestEnvironment.DocumentOf(nodeChunks.AllChunks[reference.Hash]));
            var handler = NewHandler(env);

            // Action.
            //the handler returns the stream result, then the host writes the body: the chunks are read from the node now
            var result = await handler.DownloadBytesAsync(reference, RedundancyLevel.None, RedundancyStrategy.None, false);
            await result.ExecuteAsync(env.HttpContext);

            // Assert.
            Assert.Equal(StatusCodes.Status200OK, env.HttpContext.Response.StatusCode);
            Assert.Equal(content, env.GetResponseBody());
            Assert.All(nodeChunks.AllChunks.Keys, h => Assert.Contains(env.DbChunks, d => d.Hash == h)); //every chunk read from the node was saved
        }

        // Helpers.
        private static BytesApiHandler NewHandler(HandlerTestEnvironment env) =>
            new(
                env.BeeNodeLiveManager,
                new Mock<IDataService>().Object,
                env.DbContext,
                env.HttpContextAccessor,
                NullLogger<BytesApiHandler>.Instance,
                new Mock<ISerializerModifierAccessor>().Object);

        /// <summary>
        /// The message of the error body answered
        /// </summary>
        private static string ReadErrorMessage(HandlerTestEnvironment env) =>
            JsonDocument.Parse(env.GetResponseBody()).RootElement.GetProperty("message").GetString()!;
    }
}
