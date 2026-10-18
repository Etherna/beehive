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
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.SwarmSdk.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Etherna.Beehive.Pages.Admin
{
    /// <summary>
    /// What the pins imports page shows of one node
    /// </summary>
    public sealed class PinsImportsNodeView(
        string id,
        Uri url,
        bool isAlive,
        NodePinsSnapshot? pinsSnapshot,
        string? nodeError,
        NodePinsImport? latestImport,
        IReadOnlyList<NodePinImport> latestImportRecords)
    {
        public string Id { get; } = id;
        public bool IsAlive { get; } = isAlive;
        public NodePinsImport? LatestImport { get; } = latestImport;
        public IReadOnlyList<NodePinImport> LatestImportRecords { get; } = latestImportRecords;

        /// <summary>
        /// Why the node's pins could not be listed: null when they were
        /// </summary>
        public string? NodeError { get; } = nodeError;

        /// <summary>
        /// What the node holds right now: null when the node could not be asked
        /// </summary>
        public NodePinsSnapshot? PinsSnapshot { get; } = pinsSnapshot;

        public Uri Url { get; } = url;

        // Methods.
        /// <summary>
        /// Whether a content is pinned on the node right now: null when the node could not be asked
        /// </summary>
        public bool? IsOnNode(SwarmReference reference) =>
            PinsSnapshot is null ? null : PinsSnapshot.PinnedOnNode.Contains(reference);
    }
}
