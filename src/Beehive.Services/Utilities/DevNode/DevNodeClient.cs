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

using Etherna.SwarmSdk;
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Utilities.DevNode
{
    /// <summary>
    /// Emulated bee node client for local development.
    /// Supports postage batch creation and reading with an in-memory store; any other operation
    /// fails with a <see cref="SwarmSdkApiException"/>, as an unsupported node API would do.
    /// </summary>
    internal sealed partial class DevNodeClient : ISwarmClient
    {
        // Consts.
        private static readonly ChainState DevChainState = new(
            Block: 1,
            ChainTip: 1,
            CurrentPrice: BzzValue.FromPlurLong(24_000),
            TotalAmount: BzzValue.FromPlurLong(0),
            MinimumValidityBlocks: 1);
        private static readonly Uri DevNodeUrl = new("fake://dev-node");
        public static readonly EthAddress EthereumAddress = new("0x0000000000000000000000000000000000001633");
        public const string NodeId = "dev-node";
        private const string OverlayAddress = "0000000000000000000000000000000000000000000000000000000000001633";
        private const string PssPublicKey = "030000000000000000000000000000000000000000000000000000000000001633";
        private const string PublicKey = "020000000000000000000000000000000000000000000000000000000000001633";

        // Fields.
        private readonly ConcurrentDictionary<PostageBatchId, PostageBatch> postageBatches = new();
        private readonly Lock postageBatchesWriteLock = new();

        // Properties.
        public SwarmClients ApiCompatibility => SwarmClients.Bee;
        public bool IsDryMode => true;
        public Uri NodeUrl => DevNodeUrl;

        // Methods.
        public Task<(PostageBatchId BatchId, EthTxHash TxHash)> BuyPostageBatchAsync(
            BzzValue amount,
            int depth,
            string? label = null,
            bool? immutable = null,
            ulong? gasLimit = null,
            XDaiValue? gasPrice = null,
            CancellationToken cancellationToken = default)
        {
            if (depth is < PostageBatch.MinDepth or > PostageBatch.MaxDepth)
                return Task.FromException<(PostageBatchId, EthTxHash)>(
                    NewApiException(400, "Invalid postage batch depth"));

            PostageBatchId batchId = RandomNumberGenerator.GetBytes(PostageBatchId.BatchIdSize);
            postageBatches[batchId] = new PostageBatch(
                id: batchId,
                amount: amount,
                blockNumber: DevChainState.Block,
                depth: depth,
                exists: true,
                isImmutable: immutable ?? false,
                isUsable: true,
                label: label,
                ttl: PostageBatch.CalculateTtl(amount, DevChainState.CurrentPrice),
                utilization: 0);

            return Task.FromResult((batchId, EthTxHash.Zero));
        }

        public Task<EthTxHash> DilutePostageBatchAsync(
            PostageBatchId batchId,
            int depth,
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default)
        {
            lock (postageBatchesWriteLock)
            {
                if (!postageBatches.TryGetValue(batchId, out var batch))
                    return Task.FromException<EthTxHash>(NewApiException(404, "Postage batch not found"));
                if (batch.IsImmutable)
                    return Task.FromException<EthTxHash>(NewApiException(400, "Postage batch is immutable"));
                if (depth <= batch.Depth || depth > PostageBatch.MaxDepth)
                    return Task.FromException<EthTxHash>(NewApiException(400, "Invalid postage batch depth"));

                postageBatches[batchId] = new PostageBatch(
                    id: batch.Id,
                    amount: batch.Amount,
                    blockNumber: batch.BlockNumber,
                    depth: depth,
                    exists: true,
                    isImmutable: batch.IsImmutable,
                    isUsable: true,
                    label: batch.Label,
                    ttl: batch.Ttl,
                    utilization: batch.Utilization);

                return Task.FromResult(EthTxHash.Zero);
            }
        }

        public Task<ChequebookCheque[]> GetAllChequebookChequesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<ChequebookCheque[]>([]);

        public Task<ChainState> GetChainStateAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(DevChainState);

        public Task<SwarmChunk> GetChunkAsync(
            SwarmHash hash,
            SwarmChunkBmt swarmChunkBmt,
            bool? cache = null,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromException<SwarmChunk>(NewApiException(404, "Chunk not found"));

        public Task<(PostageBatch PostageBatch, EthAddress Owner)[]> GetGlobalValidPostageBatchesAsync(
            PostageBatchId? batchId = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(postageBatches.Values
                .Where(batch => batchId is null || batch.Id == batchId)
                .Select(batch => (batch, EthereumAddress))
                .ToArray());

        public Task<Health> GetHealthAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new Health(true, "dev", "dev"));

        public Task<SwarmNodeAddresses> GetNodeAddressesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SwarmNodeAddresses(
                OverlayAddress,
                [],
                EthereumAddress,
                EthereumAddress.ToString(),
                PublicKey,
                PssPublicKey));

        public Task<PostageBatch[]> GetOwnedPostageBatchesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(postageBatches.Values.ToArray());

        public Task<PostageBatch> GetPostageBatchAsync(
            PostageBatchId batchId,
            CancellationToken cancellationToken = default) =>
            postageBatches.TryGetValue(batchId, out var batch)
                ? Task.FromResult(batch)
                : Task.FromException<PostageBatch>(NewApiException(404, "Postage batch not found"));

        public Task<PostageBucketsStatus> GetPostageBatchBucketsAsync(
            PostageBatchId batchId,
            CancellationToken cancellationToken = default)
        {
            if (!postageBatches.TryGetValue(batchId, out var batch))
                return Task.FromException<PostageBucketsStatus>(NewApiException(404, "Postage batch not found"));

            var depth = batch.Depth ?? PostageBatch.MinDepth;
            return Task.FromResult(new PostageBucketsStatus(
                PostageBatch.BucketDepth,
                1u << (depth - PostageBatch.BucketDepth),
                new uint[1 << PostageBatch.BucketDepth],
                depth));
        }

        public Task<bool> GetReadinessAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(true);

        public Task<bool> IsChunkExistingAsync(
            SwarmHash hash,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(false);

        public Task<EthTxHash> TopUpPostageBatchAsync(
            PostageBatchId batchId,
            BzzValue amount,
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default)
        {
            lock (postageBatchesWriteLock)
            {
                if (!postageBatches.TryGetValue(batchId, out var batch))
                    return Task.FromException<EthTxHash>(NewApiException(404, "Postage batch not found"));

                var newAmount = (batch.Amount ?? BzzValue.FromInt32(0)) + amount;
                postageBatches[batchId] = new PostageBatch(
                    id: batch.Id,
                    amount: newAmount,
                    blockNumber: batch.BlockNumber,
                    depth: batch.Depth,
                    exists: true,
                    isImmutable: batch.IsImmutable,
                    isUsable: true,
                    label: batch.Label,
                    ttl: PostageBatch.CalculateTtl(newAmount, DevChainState.CurrentPrice),
                    utilization: batch.Utilization);

                return Task.FromResult(EthTxHash.Zero);
            }
        }

        // Helpers.
        private static SwarmSdkApiException NewApiException(int statusCode, string message) =>
            new(message, statusCode, null, new Dictionary<string, IEnumerable<string>>(), null);
    }
}
