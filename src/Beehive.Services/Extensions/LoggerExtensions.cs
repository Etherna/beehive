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

using Etherna.SwarmSdk.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Etherna.Beehive.Services.Extensions
{
    /*
     * Always group similar log delegates by type, always use incremental event ids.
     * Last event id is: 56
     */
    public static class LoggerExtensions
    {
        // Fields.
        //*** DEBUG LOGS ***
        private static readonly Action<ILogger, string, Exception> _beeNodeLiveInstanceRemoved =
            LoggerMessage.Define<string>(
                LogLevel.Debug,
                new EventId(30, nameof(BeeNodeLiveInstanceRemoved)),
                "Node {NodeId} removed from the live instances");

        private static readonly Action<ILogger, Exception> _beeNodesHeartbeatSkipped =
            LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(31, nameof(BeeNodesHeartbeatSkipped)),
                "Bee nodes heartbeat skipped, the db could not be read");

        private static readonly Action<ILogger, SwarmHash, string, Exception> _chunkReadFromNode =
            LoggerMessage.Define<SwarmHash, string>(
                LogLevel.Debug,
                new EventId(32, nameof(ChunkReadFromNode)),
                "Chunk {Hash} read from node {NodeId}");

        private static readonly Action<ILogger, int, int, string, Exception> _chunksReadFromNode =
            LoggerMessage.Define<int, int, string>(
                LogLevel.Debug,
                new EventId(33, nameof(ChunksReadFromNode)),
                "{FoundChunks} of {RequestedChunks} chunks read from node {NodeId}");

        private static readonly Action<ILogger, Exception> _noChunksToPush =
            LoggerMessage.Define(
                LogLevel.Debug,
                new EventId(19, nameof(NoChunksToPush)),
                "No chunks to push");

        private static readonly Action<ILogger, string, bool, Exception> _resourceLockDenied =
            LoggerMessage.Define<string, bool>(
                LogLevel.Debug,
                new EventId(34, nameof(ResourceLockDenied)),
                "Lock on resource {ResourceId} denied, exclusive access: {ExclusiveAccess}");
        
        private static readonly Action<ILogger, SwarmHash, Exception> _succededToPushCac =
            LoggerMessage.Define<SwarmHash>(
                LogLevel.Debug,
                new EventId(20, nameof(SucceededToPushCac)),
                "Succeeded to push Cac with hash {Hash}");
        
        private static readonly Action<ILogger, SwarmHash, Exception> _succededToPushSoc =
            LoggerMessage.Define<SwarmHash>(
                LogLevel.Debug,
                new EventId(21, nameof(SucceededToPushSoc)),
                "Succeeded to push Soc with hash {Hash}");

        //*** INFORMATION LOGS ***
        private static readonly Action<ILogger, string, Exception> _beeNodeAlive =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(35, nameof(BeeNodeAlive)),
                "Node {NodeId} is alive");

        private static readonly Action<ILogger, int, BzzValue, Exception> _cashoutAllNodesChequesTaskCompleted =
            LoggerMessage.Define<int, BzzValue>(
                LogLevel.Information,
                new EventId(36, nameof(CashoutAllNodesChequesTaskCompleted)),
                "Cashout all nodes cheques task completed on {NodesCount} nodes, {BzzCashedOut} BZZ cashed out");

        private static readonly Action<ILogger, int, Exception> _cleanupExpiredLocksTaskCompleted =
            LoggerMessage.Define<int>(
                LogLevel.Information,
                new EventId(37, nameof(CleanupExpiredLocksTaskCompleted)),
                "Clean up expired locks task completed removing {RemovedLocks} locks");

        private static readonly Action<ILogger, long, Exception> _cleanupOldFailedTasksTaskCompleted =
            LoggerMessage.Define<long>(
                LogLevel.Information,
                new EventId(12, nameof(CleanupOldFailedTasksTaskCompleted)),
                "Clean up failed-tasks task completed removing {RemovedFailedTasks} tasks");
        
        private static readonly Action<ILogger, Exception> _cleanupOldFailedTasksTaskStarted =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(13, nameof(CleanupOldFailedTasksTaskStarted)),
                "Clean up failed-tasks task started");

        private static readonly Action<ILogger, Exception> _devNodeDisabled =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(38, nameof(DevNodeDisabled)),
                "A bee node is registered: the emulated dev node is disabled");

        private static readonly Action<ILogger, Exception> _devNodeEnabled =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(39, nameof(DevNodeEnabled)),
                "No bee node is registered: the emulated dev node is enabled");
        
        private static readonly Action<ILogger, string, BzzValue, IEnumerable<EthTxHash>, Exception> _nodeCashedOut =
            LoggerMessage.Define<string, BzzValue, IEnumerable<EthTxHash>>(
                LogLevel.Information,
                new EventId(0, nameof(NodeCashedOut)),
                "Node {BeeNodeId} cashed out {BzzCashedOut} BZZ with tx hashes {TxHashes}");

        private static readonly Action<ILogger, string, Exception> _nodeConfigurationUpdated =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(7, nameof(NodeConfigurationUpdated)),
                "Node {BeeNodeId} updated configuration");

        private static readonly Action<ILogger, string, long, long, long, long, Exception> _nodePinsImportCompleted =
            LoggerMessage.Define<string, long, long, long, long>(
                LogLevel.Information,
                new EventId(25, nameof(NodePinsImportCompleted)),
                "Node {NodeId}: pins import completed, {ImportedPins} imported, {SkippedPins} already pinned, {FailedPins} failed, {UnpinnedPins} unpinned on the node");

        private static readonly Action<ILogger, string, string, bool, int, Exception> _nodePinsImportEnqueued =
            LoggerMessage.Define<string, string, bool, int>(
                LogLevel.Information,
                new EventId(40, nameof(NodePinsImportEnqueued)),
                "Node {NodeId}: pins import enqueued as job {JobId}, unpin imported: {UnpinImported}, requested references: {RequestedReferences}");

        private static readonly Action<ILogger, string, int, Exception> _nodePinsImportsDeleted =
            LoggerMessage.Define<string, int>(
                LogLevel.Information,
                new EventId(41, nameof(NodePinsImportsDeleted)),
                "Node {NodeId}: {DeletedImports} finished pins imports deleted with their pin records");

        private static readonly Action<ILogger, string, bool, Exception> _nodePinsImportStarted =
            LoggerMessage.Define<string, bool>(
                LogLevel.Information,
                new EventId(24, nameof(NodePinsImportStarted)),
                "Node {NodeId}: pins import started, unpin imported: {UnpinImported}");

        private static readonly Action<ILogger, string, Uri, bool, Exception> _nodeRegistered =
            LoggerMessage.Define<string, Uri, bool>(
                LogLevel.Information,
                new EventId(5, nameof(NodeRegistered)),
                "Node {BeeNodeId} registered with connection string {ConnectionString}. Batch creation enabled: {IsBatchCreationEnabled}");

        private static readonly Action<ILogger, string, Exception> _nodeRemoved =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(6, nameof(NodeRemoved)),
                "Node {BeeNodeId} has been removed");

        private static readonly Action<ILogger, int, int, Exception> _nodesAddressMaintainerTaskCompleted =
            LoggerMessage.Define<int, int>(
                LogLevel.Information,
                new EventId(42, nameof(NodesAddressMaintainerTaskCompleted)),
                "Nodes address maintainer task completed funding {BzzFundedNodes} nodes with BZZ and {XDaiFundedNodes} nodes with xDai");

        private static readonly Action<ILogger, int, int, Exception> _nodesChequebookMaintainerTaskCompleted =
            LoggerMessage.Define<int, int>(
                LogLevel.Information,
                new EventId(43, nameof(NodesChequebookMaintainerTaskCompleted)),
                "Nodes chequebook maintainer task completed with {Deposits} deposits and {Withdraws} withdraws");

        private static readonly Action<ILogger, SwarmReference, Exception> _pinDeleted =
            LoggerMessage.Define<SwarmReference>(
                LogLevel.Information,
                new EventId(44, nameof(PinDeleted)),
                "Pin of {Reference} deleted");

        private static readonly Action<ILogger, SwarmReference, int, int, int, Exception> _pinProcessed =
            LoggerMessage.Define<SwarmReference, int, int, int>(
                LogLevel.Information,
                new EventId(45, nameof(PinProcessed)),
                "Pin of {Reference} processed, {PinnedChunks} chunks pinned, {MissingChunks} missing, {InvalidChunks} invalid");

        private static readonly Action<ILogger, PostageBatchId, string, int, BzzValue, bool, EthTxHash, Exception> _postageBatchBought =
            LoggerMessage.Define<PostageBatchId, string, int, BzzValue, bool, EthTxHash>(
                LogLevel.Information,
                new EventId(46, nameof(PostageBatchBought)),
                "Postage batch {PostageBatchId} bought on node {NodeId} with depth {Depth} and amount {Amount}, immutable: {Immutable}, tx hash {TxHash}");

        private static readonly Action<ILogger, PostageBatchId, string, int, EthTxHash, Exception> _postageBatchDiluted =
            LoggerMessage.Define<PostageBatchId, string, int, EthTxHash>(
                LogLevel.Information,
                new EventId(47, nameof(PostageBatchDiluted)),
                "Postage batch {PostageBatchId} diluted on node {NodeId} to depth {Depth}, tx hash {TxHash}");

        private static readonly Action<ILogger, PostageBatchId, string, BzzValue, EthTxHash, Exception> _postageBatchToppedUp =
            LoggerMessage.Define<PostageBatchId, string, BzzValue, EthTxHash>(
                LogLevel.Information,
                new EventId(48, nameof(PostageBatchToppedUp)),
                "Postage batch {PostageBatchId} topped up on node {NodeId} with amount {Amount}, tx hash {TxHash}");
        
        private static readonly Action<ILogger, Exception> _pushChunksBackgroundServiceStarted =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(14, nameof(PushChunksBackgroundServiceStarted)),
                "Push Chunks Background Service is started");
        
        private static readonly Action<ILogger, Exception> _pushChunksBackgroundServiceStopped =
            LoggerMessage.Define(
                LogLevel.Information,
                new EventId(15, nameof(PushChunksBackgroundServiceStopped)),
                "Push Chunks Background Service is stopped");

        private static readonly Action<ILogger, string, BzzValue, EthTxHash, Exception> _succededToDepositBzzOnNodeChequeBook =
            LoggerMessage.Define<string, BzzValue, EthTxHash>(
                LogLevel.Information,
                new EventId(8, nameof(SuccededToDepositBzzOnNodeChequeBook)),
                "Node {BeeNodeId} chequebook received deposit of {BzzAmount} BZZ with tx hash {TxHash}");

        private static readonly Action<ILogger, string, BzzValue, BzzValue, string, Exception> _succededToFundBzzOnNodeAddress =
            LoggerMessage.Define<string, BzzValue, BzzValue, string>(
                LogLevel.Information,
                new EventId(1, nameof(SuccededToFundBzzOnNodeAddress)),
                "Node {BeeNodeId} address funded with {BzzFunded} BZZ to total {BzzTotal} BZZ. Tx hash {TxHash}");

        private static readonly Action<ILogger, string, XDaiValue, XDaiValue, string, Exception> _succededToFundXDaiOnNodeAddress =
            LoggerMessage.Define<string, XDaiValue, XDaiValue, string>(
                LogLevel.Information,
                new EventId(3, nameof(SuccededToFundXDaiOnNodeAddress)),
                "Node {BeeNodeId} address funded with {XDaiFunded} xDai to total {XDaiTotal} xDai. Tx hash {TxHash}");

        private static readonly Action<ILogger, string, BzzValue, EthTxHash, Exception> _succededToWithdrawBzzOnNodeChequeBook =
            LoggerMessage.Define<string, BzzValue, EthTxHash>(
                LogLevel.Information,
                new EventId(10, nameof(SuccededToWithdrawBzzOnNodeChequeBook)),
                "Node {BeeNodeId} chequebook sent withdraw of {BzzAmount} BZZ with tx hash {TxHash}");

        private static readonly Action<ILogger, SwarmReference, PostageBatchId, int, bool, Exception> _uploadCompleted =
            LoggerMessage.Define<SwarmReference, PostageBatchId, int, bool>(
                LogLevel.Information,
                new EventId(49, nameof(UploadCompleted)),
                "Upload of {Reference} completed on postage batch {PostageBatchId} with {ChunksCount} chunks, pinned: {Pinned}");

        //*** WARNING LOGS ***
        private static readonly Action<ILogger, long, PostageBatchId, Exception> _abandonedPostageBatchChunks =
            LoggerMessage.Define<long, PostageBatchId>(
                LogLevel.Warning,
                new EventId(23, nameof(AbandonedPostageBatchChunks)),
                "Abandoned {ChunksCount} queued chunks of Postage Batch with Id {PostageBatchId}: its owner node reported it not found");

        private static readonly Action<ILogger, string, Exception> _beeNodeAddressesUnavailable =
            LoggerMessage.Define<string>(
                LogLevel.Warning,
                new EventId(50, nameof(BeeNodeAddressesUnavailable)),
                "Node {NodeId} addresses could not be read, the node is excluded from the nearest node selection");

        private static readonly Action<ILogger, string, string, string, Exception> _beeNodeCallFailed =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Warning,
                new EventId(51, nameof(BeeNodeCallFailed)),
                "Node {NodeId} failed the call {Operation}: {Reason}");

        private static readonly Action<ILogger, string, string, Exception> _beeNodeNotAlive =
            LoggerMessage.Define<string, string>(
                LogLevel.Warning,
                new EventId(52, nameof(BeeNodeNotAlive)),
                "Node {NodeId} is not alive: {Reason}");

        private static readonly Action<ILogger, string, string, Exception> _chequeCashoutFailed =
            LoggerMessage.Define<string, string>(
                LogLevel.Warning,
                new EventId(53, nameof(ChequeCashoutFailed)),
                "Node {NodeId}: cashout of the cheque of peer {Peer} failed");

        private static readonly Action<ILogger, string, string, Exception> _failedToReadNodeBalance =
            LoggerMessage.Define<string, string>(
                LogLevel.Warning,
                new EventId(54, nameof(FailedToReadNodeBalance)),
                "Reading the {Currency} balance of node {NodeId} address failed");

        private static readonly Action<ILogger, string, SwarmReference, Exception> _nodePinImportFailed =
            LoggerMessage.Define<string, SwarmReference>(
                LogLevel.Warning,
                new EventId(26, nameof(NodePinImportFailed)),
                "Node {NodeId}: import of pin {Reference} failed");

        private static readonly Action<ILogger, string, SwarmReference, int, int, Exception> _nodePinImportIncomplete =
            LoggerMessage.Define<string, SwarmReference, int, int>(
                LogLevel.Warning,
                new EventId(27, nameof(NodePinImportIncomplete)),
                "Node {NodeId}: import of pin {Reference} is incomplete, {MissingChunks} chunks missing and {InvalidChunks} invalid on the node");

        private static readonly Action<ILogger, string, string, string, Exception> _nodePinsImportJobGone =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Warning,
                new EventId(55, nameof(NodePinsImportJobGone)),
                "Node {NodeId}: pins import {ImportId} failed, its job {JobId} ended before completing it");

        private static readonly Action<ILogger, string, SwarmReference, Exception> _nodePinUnpinFailed =
            LoggerMessage.Define<string, SwarmReference>(
                LogLevel.Warning,
                new EventId(28, nameof(NodePinUnpinFailed)),
                "Node {NodeId}: unpin of {Reference} failed after its import");

        private static readonly Action<ILogger, PostageBatchId, Exception> _postageBatchNotFound =
            LoggerMessage.Define<PostageBatchId>(
                LogLevel.Warning,
                new EventId(22, nameof(PostageBatchNotFound)),
                "Postage Batch with Id {PostageBatchId} not found");
        
        private static readonly Action<ILogger, PostageBatchId, Exception> _postageBatchOwnerNodeNotFound =
            LoggerMessage.Define<PostageBatchId>(
                LogLevel.Warning,
                new EventId(17, nameof(PostageBatchOwnerNodeNotFound)),
                "Owner node of Postage Batch with Id {PostageBatchId} not found");

        //*** ERROR LOGS ***
        private static readonly Action<ILogger, string, BzzValue, Exception> _failedToDepositBzzOnNodeChequeBook =
            LoggerMessage.Define<string, BzzValue>(
                LogLevel.Error,
                new EventId(9, nameof(FailedToDepositBzzOnNodeChequeBook)),
                "Deposit on node {BeeNodeId} chequebook failed with {BzzAmount} BZZ");

        private static readonly Action<ILogger, string, BzzValue, string?, Exception> _failedToFundBzzOnNodeAddress =
            LoggerMessage.Define<string, BzzValue, string?>(
                LogLevel.Error,
                new EventId(2, nameof(FailedToFundBzzOnNodeAddress)),
                "Funding on node {BeeNodeId} address failed with {BzzAmount} BZZ. Tx hash {TxHash}");

        private static readonly Action<ILogger, string, XDaiValue, string?, Exception> _failedToFundXDaiOnNodeAddress =
            LoggerMessage.Define<string, XDaiValue, string?>(
                LogLevel.Error,
                new EventId(4, nameof(FailedToFundXDaiOnNodeAddress)),
                "Funding on node {BeeNodeId} address failed with {XDaiAmount} xDai. Tx hash {TxHash}");

        private static readonly Action<ILogger, SwarmHash, Exception> _failedToPushChunk =
            LoggerMessage.Define<SwarmHash>(
                LogLevel.Error,
                new EventId(18, nameof(FailedToPushChunk)),
                "Failed to push chunk with hash {Hash}");

        private static readonly Action<ILogger, SwarmHash, Exception> _failedToStoreChunk =
            LoggerMessage.Define<SwarmHash>(
                LogLevel.Error,
                new EventId(56, nameof(FailedToStoreChunk)),
                "Failed to store chunk {Hash} in the db");

        private static readonly Action<ILogger, string, BzzValue, Exception> _failedToWithdrawBzzOnNodeChequeBook =
            LoggerMessage.Define<string, BzzValue>(
                LogLevel.Error,
                new EventId(11, nameof(FailedToWithdrawBzzOnNodeChequeBook)),
                "Withdraw on node {BeeNodeId} chequebook failed with {BzzAmount} BZZ");

        private static readonly Action<ILogger, string, Exception> _nodePinsImportFailed =
            LoggerMessage.Define<string>(
                LogLevel.Error,
                new EventId(29, nameof(NodePinsImportFailed)),
                "Node {NodeId}: pins import failed");

        private static readonly Action<ILogger, Exception> _unhandledExceptionPushingChunksToNode =
            LoggerMessage.Define(
                LogLevel.Error,
                new EventId(16, nameof(UnhandledExceptionPushingChunksToNode)),
                "Unhandled exception pushing chunks to node");

        // Methods.
        public static void AbandonedPostageBatchChunks(this ILogger logger, PostageBatchId batchId, long chunksCount) =>
            _abandonedPostageBatchChunks(logger, chunksCount, batchId, null!);

        public static void BeeNodeAddressesUnavailable(this ILogger logger, string nodeId, Exception exception) =>
            _beeNodeAddressesUnavailable(logger, nodeId, exception);

        public static void BeeNodeAlive(this ILogger logger, string nodeId) =>
            _beeNodeAlive(logger, nodeId, null!);

        public static void BeeNodeCallFailed(this ILogger logger, string nodeId, string operation, string reason, Exception exception) =>
            _beeNodeCallFailed(logger, nodeId, operation, reason, exception);

        public static void BeeNodeLiveInstanceRemoved(this ILogger logger, string nodeId) =>
            _beeNodeLiveInstanceRemoved(logger, nodeId, null!);

        public static void BeeNodeNotAlive(this ILogger logger, string nodeId, string reason, Exception? exception) =>
            _beeNodeNotAlive(logger, nodeId, reason, exception!);

        public static void BeeNodesHeartbeatSkipped(this ILogger logger, Exception exception) =>
            _beeNodesHeartbeatSkipped(logger, exception);

        public static void CashoutAllNodesChequesTaskCompleted(this ILogger logger, int nodesCount, BzzValue bzzCashedOut) =>
            _cashoutAllNodesChequesTaskCompleted(logger, nodesCount, bzzCashedOut, null!);

        public static void ChequeCashoutFailed(this ILogger logger, string nodeId, string peer, Exception exception) =>
            _chequeCashoutFailed(logger, nodeId, peer, exception);

        public static void ChunkReadFromNode(this ILogger logger, SwarmHash hash, string nodeId) =>
            _chunkReadFromNode(logger, hash, nodeId, null!);

        public static void ChunksReadFromNode(this ILogger logger, int foundChunks, int requestedChunks, string nodeId) =>
            _chunksReadFromNode(logger, foundChunks, requestedChunks, nodeId, null!);

        public static void CleanupExpiredLocksTaskCompleted(this ILogger logger, int removedLocks) =>
            _cleanupExpiredLocksTaskCompleted(logger, removedLocks, null!);

        public static void CleanupOldFailedTasksTaskCompleted(this ILogger logger, long deletedTasks) =>
            _cleanupOldFailedTasksTaskCompleted(logger, deletedTasks, null!);
        
        public static void CleanupOldFailedTasksTaskStarted(this ILogger logger) =>
            _cleanupOldFailedTasksTaskStarted(logger, null!);

        public static void DevNodeDisabled(this ILogger logger) =>
            _devNodeDisabled(logger, null!);

        public static void DevNodeEnabled(this ILogger logger) =>
            _devNodeEnabled(logger, null!);

        public static void FailedToDepositBzzOnNodeChequeBook(this ILogger logger, string nodeId, BzzValue bzzDeposit, Exception exception) =>
            _failedToDepositBzzOnNodeChequeBook(logger, nodeId, bzzDeposit, exception);

        public static void FailedToFundBzzOnNodeAddress(this ILogger logger, string nodeId, BzzValue bzzFundAmount, string? txHash, Exception? exception) =>
            _failedToFundBzzOnNodeAddress(logger, nodeId, bzzFundAmount, txHash, exception!);

        public static void FailedToFundXDaiOnNodeAddress(this ILogger logger, string nodeId, XDaiValue xDaiFundAmount, string? txHash, Exception? exception) =>
            _failedToFundXDaiOnNodeAddress(logger, nodeId, xDaiFundAmount, txHash, exception!);
        
        public static void FailedToPushChunk(this ILogger logger, SwarmHash hash, Exception? exception) =>
            _failedToPushChunk(logger, hash, exception!);

        public static void FailedToReadNodeBalance(this ILogger logger, string nodeId, string currency, Exception exception) =>
            _failedToReadNodeBalance(logger, currency, nodeId, exception);

        public static void FailedToStoreChunk(this ILogger logger, SwarmHash hash, Exception exception) =>
            _failedToStoreChunk(logger, hash, exception);

        public static void FailedToWithdrawBzzOnNodeChequeBook(this ILogger logger, string nodeId, BzzValue bzzWithdraw, Exception exception) =>
            _failedToWithdrawBzzOnNodeChequeBook(logger, nodeId, bzzWithdraw, exception);

        public static void NoChunksToPush(this ILogger logger) =>
            _noChunksToPush(logger, null!);

        public static void NodeCashedOut(this ILogger logger, string beeNodeId, BzzValue bzzCashedOut, IEnumerable<EthTxHash> txHashes) =>
            _nodeCashedOut(logger, beeNodeId, bzzCashedOut, txHashes, null!);

        public static void NodeConfigurationUpdated(this ILogger logger, string beeNodeid) =>
            _nodeConfigurationUpdated(logger, beeNodeid, null!);

        public static void NodePinImportFailed(this ILogger logger, string nodeId, SwarmReference reference, Exception exception) =>
            _nodePinImportFailed(logger, nodeId, reference, exception);

        public static void NodePinImportIncomplete(this ILogger logger, string nodeId, SwarmReference reference, int missingChunks, int invalidChunks) =>
            _nodePinImportIncomplete(logger, nodeId, reference, missingChunks, invalidChunks, null!);

        public static void NodePinsImportCompleted(this ILogger logger, string nodeId, long importedPins, long skippedPins, long failedPins, long unpinnedPins) =>
            _nodePinsImportCompleted(logger, nodeId, importedPins, skippedPins, failedPins, unpinnedPins, null!);

        public static void NodePinsImportEnqueued(this ILogger logger, string nodeId, string jobId, bool unpinImported, int requestedReferences) =>
            _nodePinsImportEnqueued(logger, nodeId, jobId, unpinImported, requestedReferences, null!);

        public static void NodePinsImportFailed(this ILogger logger, string nodeId, Exception exception) =>
            _nodePinsImportFailed(logger, nodeId, exception);

        public static void NodePinsImportJobGone(this ILogger logger, string nodeId, string importId, string jobId) =>
            _nodePinsImportJobGone(logger, nodeId, importId, jobId, null!);

        public static void NodePinsImportsDeleted(this ILogger logger, string nodeId, int deletedImports) =>
            _nodePinsImportsDeleted(logger, nodeId, deletedImports, null!);

        public static void NodePinsImportStarted(this ILogger logger, string nodeId, bool unpinImported) =>
            _nodePinsImportStarted(logger, nodeId, unpinImported, null!);

        public static void NodePinUnpinFailed(this ILogger logger, string nodeId, SwarmReference reference, Exception exception) =>
            _nodePinUnpinFailed(logger, nodeId, reference, exception);

        public static void NodeRegistered(this ILogger logger, string beeNodeId, Uri connectionString, bool isBatchCreationEnabled) =>
            _nodeRegistered(logger, beeNodeId, connectionString, isBatchCreationEnabled, null!);

        public static void NodeRemoved(this ILogger logger, string beeNodeId) =>
            _nodeRemoved(logger, beeNodeId, null!);

        public static void NodesAddressMaintainerTaskCompleted(this ILogger logger, int bzzFundedNodes, int xDaiFundedNodes) =>
            _nodesAddressMaintainerTaskCompleted(logger, bzzFundedNodes, xDaiFundedNodes, null!);

        public static void NodesChequebookMaintainerTaskCompleted(this ILogger logger, int deposits, int withdraws) =>
            _nodesChequebookMaintainerTaskCompleted(logger, deposits, withdraws, null!);

        public static void PinDeleted(this ILogger logger, SwarmReference reference) =>
            _pinDeleted(logger, reference, null!);

        public static void PinProcessed(this ILogger logger, SwarmReference reference, int pinnedChunks, int missingChunks, int invalidChunks) =>
            _pinProcessed(logger, reference, pinnedChunks, missingChunks, invalidChunks, null!);

        public static void PostageBatchBought(this ILogger logger, PostageBatchId batchId, string nodeId, int depth, BzzValue amount, bool immutable, EthTxHash txHash) =>
            _postageBatchBought(logger, batchId, nodeId, depth, amount, immutable, txHash, null!);

        public static void PostageBatchDiluted(this ILogger logger, PostageBatchId batchId, string nodeId, int depth, EthTxHash txHash) =>
            _postageBatchDiluted(logger, batchId, nodeId, depth, txHash, null!);

        public static void PostageBatchNotFound(this ILogger logger, PostageBatchId batchId) =>
            _postageBatchNotFound(logger, batchId, null!);

        public static void PostageBatchOwnerNodeNotFound(this ILogger logger, PostageBatchId batchId) =>
            _postageBatchOwnerNodeNotFound(logger, batchId, null!);

        public static void PostageBatchToppedUp(this ILogger logger, PostageBatchId batchId, string nodeId, BzzValue amount, EthTxHash txHash) =>
            _postageBatchToppedUp(logger, batchId, nodeId, amount, txHash, null!);

        public static void PushChunksBackgroundServiceStarted(this ILogger logger) =>
            _pushChunksBackgroundServiceStarted(logger, null!);

        public static void PushChunksBackgroundServiceStopped(this ILogger logger) =>
            _pushChunksBackgroundServiceStopped(logger, null!);

        public static void ResourceLockDenied(this ILogger logger, string resourceId, bool exclusiveAccess) =>
            _resourceLockDenied(logger, resourceId, exclusiveAccess, null!);

        public static void SuccededToDepositBzzOnNodeChequeBook(this ILogger logger, string nodeId, BzzValue bzzDeposit, EthTxHash txHash) =>
            _succededToDepositBzzOnNodeChequeBook(logger, nodeId, bzzDeposit, txHash, null!);

        public static void SuccededToFundBzzOnNodeAddress(this ILogger logger, string nodeId, BzzValue bzzFunded, BzzValue bzzTotal, string txHash) =>
            _succededToFundBzzOnNodeAddress(logger, nodeId, bzzFunded, bzzTotal, txHash, null!);

        public static void SuccededToFundXDaiOnNodeAddress(this ILogger logger, string nodeId, XDaiValue xDaiFunded, XDaiValue xDaiTotal, string txHash) =>
            _succededToFundXDaiOnNodeAddress(logger, nodeId, xDaiFunded, xDaiTotal, txHash, null!);
        
        public static void SucceededToPushCac(this ILogger logger, SwarmHash hash) =>
            _succededToPushCac(logger, hash, null!);
        
        public static void SucceededToPushSoc(this ILogger logger, SwarmHash hash) =>
            _succededToPushSoc(logger, hash, null!);

        public static void SuccededToWithdrawBzzOnNodeChequeBook(this ILogger logger, string nodeid, BzzValue bzzWithdraw, EthTxHash txHash) =>
            _succededToWithdrawBzzOnNodeChequeBook(logger, nodeid, bzzWithdraw, txHash, null!);

        public static void UnhandledExceptionPushingChunksToNode(this ILogger logger, Exception exception) =>
            _unhandledExceptionPushingChunksToNode(logger, exception);

        public static void UploadCompleted(this ILogger logger, SwarmReference reference, PostageBatchId batchId, int chunksCount, bool pinned) =>
            _uploadCompleted(logger, reference, batchId, chunksCount, pinned, null!);
    }
}
