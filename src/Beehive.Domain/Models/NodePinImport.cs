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

using Etherna.SwarmSdk.Models;
using System;
using System.Collections.Generic;

namespace Etherna.Beehive.Domain.Models
{
    /// <summary>
    /// The import of one content pinned on a bee node, within a <see cref="NodePinsImport"/>: its outcome,
    /// what was wrong when it failed, and whether the node was unpinned afterwards
    /// </summary>
    public class NodePinImport : EntityModelBase<string>
    {
        // Fields.
        private HashSet<SwarmHash> invalidChunks = [];
        private HashSet<SwarmHash> missingChunks = [];

        // Constructors.
        public NodePinImport(NodePinsImport import, string nodeId, SwarmReference reference)
        {
            ArgumentNullException.ThrowIfNull(import);
            ArgumentNullException.ThrowIfNull(nodeId);

            Import = import;
            NodeId = nodeId;
            Reference = reference;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected NodePinImport() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        // Properties.
        /// <summary>
        /// Why the content could not be imported in full: null when it was
        /// </summary>
        public virtual string? FailureReason { get; protected set; }

        /// <summary>
        /// The chunks the node answered with a content that isn't a valid content addressed chunk
        /// </summary>
        public virtual IEnumerable<SwarmHash> InvalidChunks
        {
            get => invalidChunks;
            protected set => invalidChunks = new HashSet<SwarmHash>(value ?? []);
        }

        /// <summary>
        /// The run this record belongs to
        /// </summary>
        public virtual NodePinsImport Import { get; protected set; }

        /// <summary>
        /// The chunks the node doesn't have
        /// </summary>
        public virtual IEnumerable<SwarmHash> MissingChunks
        {
            get => missingChunks;
            protected set => missingChunks = new HashSet<SwarmHash>(value ?? []);
        }

        public virtual string NodeId { get; protected set; }
        public virtual NodePinImportOutcome Outcome =>
            FailureReason is not null ? NodePinImportOutcome.Failed :
            WasAlreadyPinned ? NodePinImportOutcome.AlreadyPinned :
            NodePinImportOutcome.Imported;
        public virtual long PinnedChunks { get; protected set; }
        public virtual SwarmReference Reference { get; protected set; }
        public virtual string? UnpinFailureReason { get; protected set; }

        /// <summary>
        /// The import asked the node to unpin the content and the node did: what the node holds now is the node's to tell
        /// </summary>
        public virtual bool UnpinnedByImport { get; protected set; }

        /// <summary>
        /// Beehive held the whole content already, nothing was read from the node
        /// </summary>
        public virtual bool WasAlreadyPinned { get; protected set; }

        // Methods.
        public virtual void SetAlreadyPinned(long pinnedChunks)
        {
            PinnedChunks = pinnedChunks;
            WasAlreadyPinned = true;
        }

        public virtual void SetFailed(
            string failureReason,
            IEnumerable<SwarmHash>? missingChunks = null,
            IEnumerable<SwarmHash>? invalidChunks = null,
            long pinnedChunks = 0)
        {
            ArgumentNullException.ThrowIfNull(failureReason);

            FailureReason = failureReason;
            InvalidChunks = invalidChunks ?? [];
            MissingChunks = missingChunks ?? [];
            PinnedChunks = pinnedChunks;
        }

        public virtual void SetImported(long pinnedChunks, IEnumerable<SwarmHash>? invalidChunks = null)
        {
            InvalidChunks = invalidChunks ?? [];
            PinnedChunks = pinnedChunks;
        }

        public virtual void SetUnpinFailed(string unpinFailureReason)
        {
            ArgumentNullException.ThrowIfNull(unpinFailureReason);

            UnpinFailureReason = unpinFailureReason;
        }

        public virtual void SetUnpinnedByImport() => UnpinnedByImport = true;
    }
}
