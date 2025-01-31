﻿// Copyright 2021-present Etherna SA
// This file is part of BeehiveManager.
// 
// BeehiveManager is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeehiveManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeehiveManager.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.BeeNet.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Etherna.BeehiveManager.Services.Extensions
{
    /*
     * Always group similar log delegates by type, always use incremental event ids.
     * Last event id is: 11
     */
    public static class LoggerExtensions
    {
        // Fields.
        //*** DEBUG LOGS ***

        //*** INFORMATION LOGS ***
        private static readonly Action<ILogger, string, BzzBalance, IEnumerable<string>, Exception> _nodeCashedOut =
            LoggerMessage.Define<string, BzzBalance, IEnumerable<string>>(
                LogLevel.Information,
                new EventId(0, nameof(NodeCashedOut)),
                "Node {BeeNodeId} cashed out {BzzCashedOut} BZZ with tx hashes {TxHashes}");

        private static readonly Action<ILogger, string, Exception> _nodeConfigurationUpdated =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(7, nameof(NodeConfigurationUpdated)),
                "Node {BeeNodeId} updated configuration");

        private static readonly Action<ILogger, string, Uri, int, bool, Exception> _nodeRegistered =
            LoggerMessage.Define<string, Uri, int, bool>(
                LogLevel.Information,
                new EventId(5, nameof(NodeRegistered)),
                "Node {BeeNodeId} registered on url {NodeUrl} on port {GatewayPort}. Batch creation enabled: {IsBatchCreationEnabled}");

        private static readonly Action<ILogger, string, Exception> _nodeRemoved =
            LoggerMessage.Define<string>(
                LogLevel.Information,
                new EventId(6, nameof(NodeRemoved)),
                "Node {BeeNodeId} has been removed");

        private static readonly Action<ILogger, string, BzzBalance, string, Exception> _succededToDepositBzzOnNodeChequeBook =
            LoggerMessage.Define<string, BzzBalance, string>(
                LogLevel.Information,
                new EventId(8, nameof(SuccededToDepositBzzOnNodeChequeBook)),
                "Node {BeeNodeId} chequebook received deposit of {BzzAmount} BZZ with tx hash {TxHash}");

        private static readonly Action<ILogger, string, BzzBalance, BzzBalance, string, Exception> _succededToFundBzzOnNodeAddress =
            LoggerMessage.Define<string, BzzBalance, BzzBalance, string>(
                LogLevel.Information,
                new EventId(1, nameof(SuccededToFundBzzOnNodeAddress)),
                "Node {BeeNodeId} address funded with {BzzFunded} BZZ to total {BzzTotal} BZZ. Tx hash {TxHash}");

        private static readonly Action<ILogger, string, XDaiBalance, XDaiBalance, string, Exception> _succededToFundXDaiOnNodeAddress =
            LoggerMessage.Define<string, XDaiBalance, XDaiBalance, string>(
                LogLevel.Information,
                new EventId(3, nameof(SuccededToFundXDaiOnNodeAddress)),
                "Node {BeeNodeId} address funded with {XDaiFunded} xDai to total {XDaiTotal} xDai. Tx hash {TxHash}");

        private static readonly Action<ILogger, string, BzzBalance, string, Exception> _succededToWithdrawBzzOnNodeChequeBook =
            LoggerMessage.Define<string, BzzBalance, string>(
                LogLevel.Information,
                new EventId(10, nameof(SuccededToWithdrawBzzOnNodeChequeBook)),
                "Node {BeeNodeId} chequebook sent withdraw of {BzzAmount} BZZ with tx hash {TxHash}");

        //*** WARNING LOGS ***

        //*** ERROR LOGS ***
        private static readonly Action<ILogger, string, BzzBalance, Exception> _failedToDepositBzzOnNodeChequeBook =
            LoggerMessage.Define<string, BzzBalance>(
                LogLevel.Error,
                new EventId(9, nameof(FailedToDepositBzzOnNodeChequeBook)),
                "Deposit on node {BeeNodeId} chequebook failed with {BzzAmount} BZZ");

        private static readonly Action<ILogger, string, BzzBalance, string?, Exception> _failedToFundBzzOnNodeAddress =
            LoggerMessage.Define<string, BzzBalance, string?>(
                LogLevel.Error,
                new EventId(2, nameof(FailedToFundBzzOnNodeAddress)),
                "Funding on node {BeeNodeId} address failed with {BzzAmount} BZZ. Tx hash {TxHash}");

        private static readonly Action<ILogger, string, XDaiBalance, string?, Exception> _failedToFundXDaiOnNodeAddress =
            LoggerMessage.Define<string, XDaiBalance, string?>(
                LogLevel.Error,
                new EventId(4, nameof(FailedToFundXDaiOnNodeAddress)),
                "Funding on node {BeeNodeId} address failed with {XDaiAmount} xDai. Tx hash {TxHash}");

        private static readonly Action<ILogger, string, BzzBalance, Exception> _failedToWithdrawBzzOnNodeChequeBook =
            LoggerMessage.Define<string, BzzBalance>(
                LogLevel.Error,
                new EventId(11, nameof(FailedToWithdrawBzzOnNodeChequeBook)),
                "Withdraw on node {BeeNodeId} chequebook failed with {BzzAmount} BZZ");

        // Methods.
        public static void FailedToDepositBzzOnNodeChequeBook(this ILogger logger, string nodeId, BzzBalance bzzDeposit, Exception exception) =>
            _failedToDepositBzzOnNodeChequeBook(logger, nodeId, bzzDeposit, exception);

        public static void FailedToFundBzzOnNodeAddress(this ILogger logger, string nodeId, BzzBalance bzzFundAmount, string? txHash, Exception? exception) =>
            _failedToFundBzzOnNodeAddress(logger, nodeId, bzzFundAmount, txHash, exception!);

        public static void FailedToFundXDaiOnNodeAddress(this ILogger logger, string nodeId, XDaiBalance xDaiFundAmount, string? txHash, Exception? exception) =>
            _failedToFundXDaiOnNodeAddress(logger, nodeId, xDaiFundAmount, txHash, exception!);

        public static void FailedToWithdrawBzzOnNodeChequeBook(this ILogger logger, string nodeId, BzzBalance bzzWithdraw, Exception exception) =>
            _failedToWithdrawBzzOnNodeChequeBook(logger, nodeId, bzzWithdraw, exception);

        public static void NodeCashedOut(this ILogger logger, string beeNodeId, BzzBalance bzzCashedOut, IEnumerable<string> txHashes) =>
            _nodeCashedOut(logger, beeNodeId, bzzCashedOut, txHashes, null!);

        public static void NodeConfigurationUpdated(this ILogger logger, string beeNodeid) =>
            _nodeConfigurationUpdated(logger, beeNodeid, null!);

        public static void NodeRegistered(this ILogger logger, string beeNodeId, Uri nodeUrl, int gatewayPort, bool isBatchCreationEnabled) =>
            _nodeRegistered(logger, beeNodeId, nodeUrl, gatewayPort, isBatchCreationEnabled, null!);

        public static void NodeRemoved(this ILogger logger, string beeNodeId) =>
            _nodeRemoved(logger, beeNodeId, null!);

        public static void SuccededToDepositBzzOnNodeChequeBook(this ILogger logger, string nodeId, BzzBalance bzzDeposit, string txHash) =>
            _succededToDepositBzzOnNodeChequeBook(logger, nodeId, bzzDeposit, txHash, null!);

        public static void SuccededToFundBzzOnNodeAddress(this ILogger logger, string nodeId, BzzBalance bzzFunded, BzzBalance bzzTotal, string txHash) =>
            _succededToFundBzzOnNodeAddress(logger, nodeId, bzzFunded, bzzTotal, txHash, null!);

        public static void SuccededToFundXDaiOnNodeAddress(this ILogger logger, string nodeId, XDaiBalance xDaiFunded, XDaiBalance xDaiTotal, string txHash) =>
            _succededToFundXDaiOnNodeAddress(logger, nodeId, xDaiFunded, xDaiTotal, txHash, null!);

        public static void SuccededToWithdrawBzzOnNodeChequeBook(this ILogger logger, string nodeid, BzzBalance bzzWithdraw, string txHash) =>
            _succededToWithdrawBzzOnNodeChequeBook(logger, nodeid, bzzWithdraw, txHash, null!);
    }
}
