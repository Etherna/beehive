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

using Etherna.Beehive.Services.Utilities.Models;
using Etherna.SwarmSdk;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Helpers
{
    /// <summary>
    /// An alive bee node serving the chunks it holds and answering bee's 404 for the others
    /// </summary>
    internal sealed class FakeBeeNode
    {
        // Constructor.
        public FakeBeeNode(string nodeId, params SwarmChunk[] chunks)
        {
            foreach (var chunk in chunks)
                Chunks[chunk.Hash] = chunk;

            ClientMock.Setup(c => c.GetChunkAsync(
                    It.IsAny<SwarmHash>(),
                    It.IsAny<SwarmChunkBmt>(),
                    It.IsAny<bool?>(),
                    It.IsAny<long?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .Returns((SwarmHash hash, SwarmChunkBmt _, bool? _, long? _, string? _, string? _, CancellationToken _) =>
                    Chunks.TryGetValue(hash, out var chunk)
                        ? Task.FromResult(chunk)
                        : Task.FromException<SwarmChunk>(new SwarmSdkApiException(
                            "The HTTP status code of the response was not expected (404).",
                            404,
                            """{"code":404,"message":"chunk not found"}""",
                            new Dictionary<string, IEnumerable<string>>(),
                            null)));

            Node = new BeeNodeLiveInstance(nodeId, ClientMock.Object, true, NullLogger.Instance);
            Node.Status.SucceededHeartbeatAttempt(DateTime.UtcNow);
        }

        // Properties.
        public Dictionary<SwarmHash, SwarmChunk> Chunks { get; } = [];
        public Mock<ISwarmClient> ClientMock { get; } = new();
        public BeeNodeLiveInstance Node { get; }
    }
}
