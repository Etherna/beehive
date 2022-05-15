using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Etherna.BeehiveManager.Services.Utilities.Models
{
    [SuppressMessage("Performance", "CA1815:Override equals and operator equals on value types", Justification = "Status comparison is not a required function")]
    public struct BeeNodeStatus
    {
        // Properties.
        public IEnumerable<string>? Errors { get; internal init; }
        public bool IsAlive { get; internal init; }
        public IEnumerable<string>? PostageBatchesId { get; internal init; }
    }
}
