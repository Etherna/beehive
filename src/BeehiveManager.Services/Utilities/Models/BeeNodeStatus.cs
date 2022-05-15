using System;
using System.Collections.Generic;

namespace Etherna.BeehiveManager.Services.Utilities.Models
{
    public class BeeNodeStatus
    {
        // Properties.
        public IEnumerable<string> Errors { get; internal set; } = Array.Empty<string>();
        public bool IsAlive { get; internal set; }
        public bool IsInitialized { get; internal set; }
        public IEnumerable<PostageBatch> PostageBatches { get; internal set; } = Array.Empty<PostageBatch>();
    }
}
