using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;

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

        private static readonly Action<ILogger, string, string, string, Exception> _succededToFundBzzOnNode =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Information,
                new EventId(1, nameof(SuccededToFundBzzOnNode)),
                "Node {BeeNodeId} funded with {PlurFunded} PLUR to final {PlurFinal} PLUR");

        private static readonly Action<ILogger, string, string, string, Exception> _succededToFundXDaiOnNode =
            LoggerMessage.Define<string, string, string>(
                LogLevel.Information,
                new EventId(3, nameof(SuccededToFundXDaiOnNode)),
                "Node {BeeNodeId} funded with {WeiFunded} xDai Wei to final {WeiFinal} xDai Wei");

        //*** WARNING LOGS ***

        //*** ERROR LOGS ***
        private static readonly Action<ILogger, string, string, Exception> _failedToFundBzzOnNode =
            LoggerMessage.Define<string, string>(LogLevel.Error,
                new EventId(2, nameof(FailedToFundBzzOnNode)),
                "Funding on node {BeeNodeId} failed with tx of {PlurAmount} PLUR");

        private static readonly Action<ILogger, string, string, Exception> _failedToFundXDaiOnNode =
            LoggerMessage.Define<string, string>(LogLevel.Error,
                new EventId(2, nameof(FailedToFundXDaiOnNode)),
                "Funding on node {BeeNodeId} failed with tx of {WeiAmount} xDai Wei");

        // Methods.
        public static void FailedToFundBzzOnNode(this ILogger logger, string nodeId, BigInteger plurFundAmount, Exception? exception) =>
            _failedToFundBzzOnNode(logger, nodeId, plurFundAmount.ToString(CultureInfo.InvariantCulture), exception!);

        public static void FailedToFundXDaiOnNode(this ILogger logger, string nodeId, BigInteger weiFundAmount, Exception? exception) =>
            _failedToFundXDaiOnNode(logger, nodeId, weiFundAmount.ToString(CultureInfo.InvariantCulture), exception!);

        public static void NodeCashedOut(this ILogger logger, string beeNodeId, long totalCashedOut, IEnumerable<string> txHashes) =>
            _nodeCashedOut(logger, beeNodeId, totalCashedOut, txHashes, null!);

        public static void SuccededToFundBzzOnNode(this ILogger logger, string nodeId, BigInteger plurFunded, BigInteger plurFinal) =>
            _succededToFundBzzOnNode(logger, nodeId, plurFunded.ToString(CultureInfo.InvariantCulture), plurFinal.ToString(CultureInfo.InvariantCulture), null!);

        public static void SuccededToFundXDaiOnNode(this ILogger logger, string nodeId, BigInteger weiFunded, BigInteger weiFinal) =>
            _succededToFundXDaiOnNode(logger, nodeId, weiFunded.ToString(CultureInfo.InvariantCulture), weiFinal.ToString(CultureInfo.InvariantCulture), null!);
    }
}
