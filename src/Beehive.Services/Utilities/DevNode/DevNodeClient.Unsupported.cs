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

using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Tools;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Utilities.DevNode
{
    /* Members not supported by the dev node emulation.
     * They fail with a SwarmSdkApiException, already handled by all node API consumers. */
    internal sealed partial class DevNodeClient
    {
        // Methods.
        public Task<Dictionary<string, Account>> AccountingAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<EthTxHash> CashoutChequeForPeerAsync(
            string peerId,
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<CheckPinsResult> CheckPinsAsync(
            SwarmReference? reference,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task ChunksBulkUploadAsync(
            SwarmChunk[] chunks,
            PostageBatchId batchId,
            int maxUploadAttempts = 1,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<SwarmOverlayAddress> ConnectToPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<bool> CreatePinAsync(
            SwarmReference reference,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<TagInfo> CreateTagAsync(
            SwarmHash hash,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<DebugChunkStore> DebugChunkStoreAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task DeletePeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task DeletePinAsync(
            SwarmReference reference,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task DeleteTagAsync(
            TagId id,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<EthTxHash> DeleteTransactionAsync(
            EthTxHash txHash,
            XDaiValue? gasPrice = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<EthTxHash> DepositIntoChequebookAsync(
            BzzValue amount,
            XDaiValue? gasPrice = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<EnvelopeResponse> EnvelopeAsync(
            string address,
            PostageBatchId batchId,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<PeerBalance[]> GetAllBalancesAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<IEnumerable<PeerBalance>> GetAllConsumedBalancesAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<(string Address, bool FullNode)[]> GetAllPeerAddressesAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<SwarmReference[]> GetAllPinsAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<Settlement> GetAllSettlementsAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<Settlement> GetAllTimeSettlementsAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<PeerBalance> GetBalanceWithPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<BlockedPeer[]> GetBlocklistedPeerAddressesAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<Stream> GetBytesAsync(
            SwarmReference reference,
            bool? cache = null,
            RedundancyLevel? redundancyLevel = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyFallbackMode = null,
            string? chunkRetrievalTimeout = null,
            int? lookaheadBufferSize = null,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<HttpContentHeaders?> GetBytesHeadersAsync(
            SwarmReference reference,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<EthAddress> GetChequebookAddressAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<ChequebookBalance> GetChequebookBalanceAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<ChequebookCashout> GetChequebookCashoutForPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<ChequebookCheque> GetChequebookChequeForPeerAsync(
            string peerId,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<Stream> GetChunkStreamAsync(
            SwarmHash hash,
            bool? cache = null,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task GetChunksStreamWebSocketAsync(
            ulong? tagHeader = null,
            ulong? tagQuery = null,
            PostageBatchId? batchId = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<IChunkWebSocketUploader> GetChunkUploaderWebSocketAsync(
            PostageBatchId batchId,
            TagId? tagId = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<PeerBalance> GetConsumedBalanceWithPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<FileResponse> GetFileAsync(
            SwarmAddress address,
            bool? cache = null,
            RedundancyLevel? redundancyLevel = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyFallbackMode = null,
            string? chunkRetrievalTimeout = null,
            int? lookaheadBufferSize = null,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<NeighborhoodStatus[]> GetNeighborhoodsStatus(
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<NodeInfo> GetNodeInfoAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<GnosisChainTx[]> GetPendingTransactionsAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<PinStatus> GetPinStatusAsync(
            SwarmReference reference,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<ReserveCommitment> GetReserveCommitmentAsync(
            int depth,
            string anchor1,
            string anchor2,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<ReserveState> GetReserveStateAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<SettlementData> GetSettlementsWithPeerAsync(
            string peerAddress,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<SwarmSoc> GetSocAsync(
            EthAddress owner,
            SwarmSocIdentifier identifier,
            bool? cache = null,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<FileResponse> GetSocDataAsync(
            EthAddress owner,
            string id,
            bool? onlyRootChunk = null,
            bool? cache = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyFallbackMode = null,
            string? chunkRetrievalTimeout = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<Topology> GetSwarmTopologyAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<TagInfo> GetTagInfoAsync(
            TagId id,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<TagInfo[]> GetTagsListAsync(
            int? offset = null,
            int? limit = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<GnosisChainTx> GetTransactionInfoAsync(
            EthTxHash txHash,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<WalletBalances> GetWalletBalance(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<string> GetWelcomeMessageAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<ICollection<string>> GranteeGetAsync(string reference, CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<GranteeResponse> GranteePatchAsync(string reference, string actHistoryAddress, PostageBatchId batchId, string[] addList, string[] revokeList, TagId? tagId = null, bool? pin = null, bool? deferredUpload = null, CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<GranteeResponse> GranteePostAsync(PostageBatchId batchId, string[] grantees, TagId? tagId = null, bool? pin = null, bool? deferredUpload = null, string? actHistoryAddress = null, CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<bool> IsContentRetrievableAsync(
            SwarmReference reference,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<LogData> LoggersGetAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<LogData> LoggersGetAsync(string exp, CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task LoggersPutAsync(string exp, Verbosity verbosity, CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<WebSocket> OpenChunkUploadWebSocketConnectionAsync(
            string endpointPath,
            PostageBatchId batchId,
            TagId? tagId,
            int internalBufferSize,
            int receiveBufferSize,
            int sendBufferSize,
            CancellationToken cancellationToken) =>
            throw NewUnsupportedException();

        public Task<string> RebroadcastTransactionAsync(
            EthTxHash txHash,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<RedistributionState> RedistributionStateAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task ReuploadContentAsync(
            SwarmReference reference,
            PostageBatchId batchId,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task SendPssAsync(
            string topic,
            string targets,
            PostageBatchId batchId,
            string? recipient = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task SetWelcomeMessageAsync(
            string welcomeMessage,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task StakeDeleteAsync(
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task StakeGetAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task StakePostAsync(
            BzzValue amount,
            XDaiValue? gasPrice = null,
            ulong? gasLimit = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task StakeWithdrawableDeleteAsync(XDaiValue? gasPrice = null, ulong? gasLimit = null, CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task StakeWithdrawableGetAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<StatusNode> StatusNodeAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<StatusNode[]> StatusPeersAsync(CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task SubscribeToGsocAsync(
            string reference,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task SubscribeToPssAsync(
            string topic,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<string> TryConnectToPeerAsync(
            string peerId,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<FileResponse?> TryGetFeedAsync(
            EthAddress owner,
            SwarmFeedTopic topic,
            long? at = null,
            ulong? after = null,
            int? afterLevel = null,
            SwarmFeedType type = SwarmFeedType.Sequence,
            bool? onlyRootChunk = null,
            bool? cache = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyFallbackMode = null,
            string? chunkRetrievalTimeout = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<HttpContentHeaders?> TryGetFileHeadersAsync(
            SwarmAddress address,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            RedundancyLevel? redundancyLevel = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyStrategyFallback = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<long?> TryGetFileSizeAsync(
            SwarmAddress address,
            long? actTimestamp = null,
            string? actPublisher = null,
            string? actHistoryAddress = null,
            RedundancyLevel? redundancyLevel = null,
            RedundancyStrategy? redundancyStrategy = null,
            bool? redundancyStrategyFallback = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<SwarmHash> UpdateFeedAsync(
            SwarmFeedTopic topic,
            SwarmFeedType type,
            ReadOnlyMemory<byte> data,
            ISigner signer,
            PostageBatchId batchId,
            SwarmFeedIndexBase? knownNearIndex = null,
            TagId? tagId = null,
            bool deferredUpload = false,
            bool? pin = null,
            DateTimeOffset? timestamp = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task UpdateTagAsync(
            TagId id,
            SwarmHash? hash = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<SwarmReference> UploadBytesAsync(
            Stream body,
            PostageBatchId batchId,
            ushort? compactLevel = 0,
            TagId? tagId = null,
            bool? pin = null,
            bool? encrypt = null,
            bool? act = null,
            string? actHistoryAddress = null,
            bool? deferredUpload = null,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            int maxUploadAttempts = 1,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<SwarmHash> UploadChunkAsync(
            Stream chunkData,
            PostageBatchId? batchId,
            TagId? tagId = null,
            PostageStamp? presignedPostageStamp = null,
            bool? act = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<SwarmHash> UploadChunkAsync(
            SwarmCac chunk,
            PostageBatchId? batchId,
            TagId? tagId = null,
            PostageStamp? presignedPostageStamp = null,
            bool? act = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<SwarmReference> UploadDirectoryAsync(
            string directoryPath,
            PostageBatchId batchId,
            ushort? compactLevel = 0,
            TagId? tagId = null,
            bool? pin = null,
            bool? encrypt = null,
            string? indexDocument = null,
            string? errorDocument = null,
            bool? deferredUpload = null,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            bool? act = null,
            string? actHistoryAddress = null,
            int maxUploadAttempts = 1,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<SwarmHash> UploadFeedManifestAsync(
            SwarmFeedBase feed,
            PostageBatchId batchId,
            ushort? compactLevel = 0,
            bool pin = false,
            bool? act = null,
            string? actHistoryAddress = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<SwarmReference> UploadFileAsync(
            Stream content,
            PostageBatchId batchId,
            ushort? compactLevel = 0,
            string? name = null,
            string? contentType = null,
            bool isFileCollection = false,
            TagId? tagId = null,
            bool? pin = null,
            bool? encrypt = null,
            string? indexDocument = null,
            string? errorDocument = null,
            bool? deferredUpload = null,
            RedundancyLevel redundancyLevel = RedundancyLevel.None,
            int maxUploadAttempts = 1,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<SwarmHash> UploadSocAsync(
            SwarmSoc soc,
            PostageBatchId? batchId,
            PostageStamp? presignedPostageStamp = null,
            TagId? tagId = null,
            bool deferredUpload = false,
            bool? act = null,
            string? actHistoryAddress = null,
            bool? pin = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<EthTxHash> WalletBzzWithdrawAsync(
            BzzValue amount,
            EthAddress address,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<EthTxHash> WalletNativeCoinWithdrawAsync(
            XDaiValue amount,
            EthAddress address,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        public Task<EthTxHash> WithdrawFromChequebookAsync(
            BzzValue amount,
            XDaiValue? gasPrice = null,
            CancellationToken cancellationToken = default) =>
            throw NewUnsupportedException();

        // Helpers.
        private static SwarmSdkApiException NewUnsupportedException() =>
            NewApiException(501, "Not supported by Beehive dev node");
    }
}
