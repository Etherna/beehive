using Etherna.BeehiveManager.Services.Utilities.Models;
using System;
using System.Collections.Generic;

namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public class BeeNodeStatusDto
    {
        // Constructor.
        public BeeNodeStatusDto(string id, BeeNodeStatus status)
        {
            Errors = status.Errors ?? Array.Empty<string>();
            IsAlive = status.IsAlive;
            PostageBatchesId = status.PostageBatchesId ?? Array.Empty<string>();
            Id = id;
        }

        // Properties.
        public string Id { get; }
        public IEnumerable<string> Errors { get; }
        public bool IsAlive { get; }
        public IEnumerable<string> PostageBatchesId { get; }
    }
}
