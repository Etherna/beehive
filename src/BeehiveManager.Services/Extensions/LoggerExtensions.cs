using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;

namespace Etherna.BeehiveManager.Services.Extensions
{
    /*
     * Always group similar log delegates by type, always use incremental event ids.
     * Last event id is: 0
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
                "Node '{BeeNodeId}' cashed out {TotalCashedOut} with tx hashes {TxHashes}");

        //*** WARNING LOGS ***

        //*** ERROR LOGS ***

        // Methods.
        public static void NodeCashedOut(this ILogger logger, string beeNodeId, long totalCashedOut, IEnumerable<string> txHashes) =>
            _nodeCashedOut(logger, beeNodeId, totalCashedOut, txHashes, null!);
    }
}
