//   Copyright 2021-present Etherna Sagl
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Etherna.BeehiveManager.Services.Extensions
{
    /*
     * Always group similar log delegates by type, always use incremental event ids.
     * Last event id is: 4
     */
    public static class LoggerExtensions
    {
        // Fields.
        //*** DEBUG LOGS ***

        //*** INFORMATION LOGS ***
        private static readonly Action<ILogger, string, long, IEnumerable<string>, Exception> _nodeCashedOut =
            LoggerMessage.Define<string, long, IEnumerable<string>>(
                LogLevel.Information,
                new EventId(0, nameof(NodeCashedOut)),
                "Node {BeeNodeId} cashed out {TotalCashedOut} with tx hashes {TxHashes}");

        private static readonly Action<ILogger, string, decimal, decimal, string, Exception> _succededToFundBzzOnNode =
            LoggerMessage.Define<string, decimal, decimal, string>(
                LogLevel.Information,
                new EventId(1, nameof(SuccededToFundBzzOnNode)),
                "Node {BeeNodeId} funded with {BzzFunded} BZZ to total {BzzTotal} BZZ. Tx hash {TxHash}");

        private static readonly Action<ILogger, string, decimal, decimal, string, Exception> _succededToFundXDaiOnNode =
            LoggerMessage.Define<string, decimal, decimal, string>(
                LogLevel.Information,
                new EventId(3, nameof(SuccededToFundXDaiOnNode)),
                "Node {BeeNodeId} funded with {XDaiFunded} xDai to total {XDaiTotal} xDai. Tx hash {TxHash}");

        //*** WARNING LOGS ***

        //*** ERROR LOGS ***
        private static readonly Action<ILogger, string, decimal, string?, Exception> _failedToFundBzzOnNode =
            LoggerMessage.Define<string, decimal, string?>(LogLevel.Error,
                new EventId(2, nameof(FailedToFundBzzOnNode)),
                "Funding on node {BeeNodeId} failed with {BzzAmount} BZZ. Tx hash {TxHash}");

        private static readonly Action<ILogger, string, decimal, string?, Exception> _failedToFundXDaiOnNode =
            LoggerMessage.Define<string, decimal, string?>(LogLevel.Error,
                new EventId(2, nameof(FailedToFundXDaiOnNode)),
                "Funding on node {BeeNodeId} failed with {XDaiAmount} xDai. Tx hash {TxHash}");

        // Methods.
        public static void FailedToFundBzzOnNode(this ILogger logger, string nodeId, decimal bzzFundAmount, string? txHash, Exception? exception) =>
            _failedToFundBzzOnNode(logger, nodeId, bzzFundAmount, txHash, exception!);

        public static void FailedToFundXDaiOnNode(this ILogger logger, string nodeId, decimal xDaiFundAmount, string? txHash, Exception? exception) =>
            _failedToFundXDaiOnNode(logger, nodeId, xDaiFundAmount, txHash, exception!);

        public static void NodeCashedOut(this ILogger logger, string beeNodeId, long totalCashedOut, IEnumerable<string> txHashes) =>
            _nodeCashedOut(logger, beeNodeId, totalCashedOut, txHashes, null!);

        public static void SuccededToFundBzzOnNode(this ILogger logger, string nodeId, decimal bzzFunded, decimal bzzTotal, string txHash) =>
            _succededToFundBzzOnNode(logger, nodeId, bzzFunded, bzzTotal, txHash, null!);

        public static void SuccededToFundXDaiOnNode(this ILogger logger, string nodeId, decimal xDaiFunded, decimal xDaiTotal, string txHash) =>
            _succededToFundXDaiOnNode(logger, nodeId, xDaiFunded, xDaiTotal, txHash, null!);
    }
}
