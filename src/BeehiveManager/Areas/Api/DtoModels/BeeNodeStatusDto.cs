using Etherna.BeehiveManager.Services.Utilities.Models;
using System;
using System.Collections.Generic;

namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public class BeeNodeStatusDto
    {
        // Constructor.
        public BeeNodeStatusDto(BeeNodeStatus status)
        {
            Errors = status.Errors ?? Array.Empty<string>();
            IsAlive = status.IsAlive;
            PostageBatchesId = status.PostageBatchesId ?? Array.Empty<string>();
        }

        // Properties.
        public IEnumerable<string> Errors { get; }
        public bool IsAlive { get; }
        public IEnumerable<string> PostageBatchesId { get; }
    }
}
