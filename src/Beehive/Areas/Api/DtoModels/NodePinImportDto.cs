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

using Etherna.Beehive.Domain.Models;
using Etherna.SwarmSdk.Models;
using System;
using System.Collections.Generic;

namespace Etherna.Beehive.Areas.Api.DtoModels
{
    public sealed class NodePinImportDto(
        DateTime creationDateTime,
        string? failureReason,
        string id,
        string importId,
        IEnumerable<SwarmHash> invalidChunks,
        bool? isOnNode,
        IEnumerable<SwarmHash> missingChunks,
        string nodeId,
        NodePinImportOutcome outcome,
        long pinnedChunks,
        SwarmReference reference,
        string? unpinFailureReason,
        bool unpinnedByImport)
    {
        public string Id { get; } = id;
        public string ImportId { get; } = importId;
        public string NodeId { get; } = nodeId;
        public SwarmReference Reference { get; } = reference;
        public NodePinImportOutcome Outcome { get; } = outcome;
        public long PinnedChunks { get; } = pinnedChunks;
        public IEnumerable<SwarmHash> MissingChunks { get; } = missingChunks;
        public IEnumerable<SwarmHash> InvalidChunks { get; } = invalidChunks;
        public string? FailureReason { get; } = failureReason;
        public bool UnpinnedByImport { get; } = unpinnedByImport;
        public string? UnpinFailureReason { get; } = unpinFailureReason;

        /// <summary>
        /// Whether the content is pinned on the node right now: null when the node could not be asked
        /// </summary>
        public bool? IsOnNode { get; } = isOnNode;

        public DateTime CreationDateTime { get; } = creationDateTime;
    }
}
