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
    public sealed class NodePinsImportDto(
        DateTime? completedDateTime,
        DateTime creationDateTime,
        string? error,
        long failedPins,
        string id,
        long importedPins,
        string? jobId,
        string nodeId,
        IEnumerable<SwarmReference> requestedReferences,
        long skippedPins,
        DateTime? startedDateTime,
        NodePinsImportState state,
        long? totalPins,
        bool unpinImported,
        long unpinnedPins)
    {
        public string Id { get; } = id;
        public string NodeId { get; } = nodeId;
        public NodePinsImportState State { get; } = state;
        public bool UnpinImported { get; } = unpinImported;

        /// <summary>
        /// The contents the import was asked for: empty when it takes every pin of the node
        /// </summary>
        public IEnumerable<SwarmReference> RequestedReferences { get; } = requestedReferences;

        public long? TotalPins { get; } = totalPins;
        public long ImportedPins { get; } = importedPins;
        public long SkippedPins { get; } = skippedPins;
        public long FailedPins { get; } = failedPins;
        public long UnpinnedPins { get; } = unpinnedPins;
        public string? Error { get; } = error;
        public string? JobId { get; } = jobId;
        public DateTime CreationDateTime { get; } = creationDateTime;
        public DateTime? StartedDateTime { get; } = startedDateTime;
        public DateTime? CompletedDateTime { get; } = completedDateTime;
    }
}
