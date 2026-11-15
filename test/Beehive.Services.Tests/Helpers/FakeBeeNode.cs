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
using Etherna.SwarmSdk.Stores;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Helpers
{
    /// <summary>
    /// A bee node alive for the live manager, serving the chunks of its in-memory store and answering bee's
    /// error for the others: not found by default, or the given status for a node that fails its reads
    /// </summary>
    internal sealed class FakeBeeNode
    {
        // Consts.
        private const string Ethereum = "0x0000000000000000000000000000000000000001";
        private const string PublicKey = "02" + "0000000000000000000000000000000000000000000000000000000000000001";

        // Constructor.
        /// <param name="missingChunkStatusCode">The status the node answers for a chunk it doesn't hold</param>
        public FakeBeeNode(string nodeId, SwarmOverlayAddress overlay, int missingChunkStatusCode = 404)
        {
            ClientMock.Setup(c => c.GetChunkAsync(
                    It.IsAny<SwarmHash>(),
                    It.IsAny<SwarmChunkBmt>(),
                    It.IsAny<bool?>(),
                    It.IsAny<long?>(),
                    It.IsAny<string?>(),
                    It.IsAny<string?>(),
                    It.IsAny<CancellationToken>()))
                .Returns(async (
                    SwarmHash hash,
                    SwarmChunkBmt _,
                    bool? _,
                    long? _,
                    string? _,
                    string? _,
                    CancellationToken cancellationToken) =>
                {
                    requestedHashes.Enqueue(hash);
                    return await Chunks.TryGetAsync(hash, cancellationToken) ??
                        throw new SwarmSdkApiException(
                            string.Format(CultureInfo.InvariantCulture,
                                "The HTTP status code of the response was not expected ({0}).", missingChunkStatusCode),
                            missingChunkStatusCode,
                            string.Format(CultureInfo.InvariantCulture,
                                """{{"code":{0},"message":"chunk {1}"}}""", missingChunkStatusCode, hash),
                            new Dictionary<string, IEnumerable<string>>(),
                            null);
                });

            Instance = new BeeNodeLiveInstance(nodeId, ClientMock.Object, true, NullLogger.Instance);
            Instance.Status.SucceededHeartbeatAttempt(DateTime.UtcNow);
            Instance.Status.InitializeAddresses(new BeeNodeAddresses(Ethereum, overlay, PublicKey, PublicKey));
        }

        // Fields.
        private readonly ConcurrentQueue<SwarmHash> requestedHashes = new();

        // Properties.
        /// <summary>
        /// The chunks the node holds
        /// </summary>
        public MemoryChunkStore Chunks { get; } = new();
        public Mock<ISwarmClient> ClientMock { get; } = new();
        public BeeNodeLiveInstance Instance { get; }

        /// <summary>
        /// The hashes asked to the node, in order of request
        /// </summary>
        public IEnumerable<SwarmHash> RequestedHashes => requestedHashes;
    }
}
