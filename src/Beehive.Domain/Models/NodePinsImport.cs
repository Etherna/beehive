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
    /// The import of the contents pinned on a bee node into beehive's own pins: one record per node and run,
    /// over every pin of the node or over the requested references only
    /// </summary>
    public class NodePinsImport : EntityModelBase<string>
    {
        // Fields.
        private HashSet<SwarmReference> requestedReferences = [];

        // Constructors.
        /// <param name="requestedReferences">The contents to import: null or empty for every pin of the node</param>
        public NodePinsImport(
            string nodeId,
            bool unpinImported,
            IEnumerable<SwarmReference>? requestedReferences = null)
        {
            ArgumentNullException.ThrowIfNull(nodeId);

            NodeId = nodeId;
            RequestedReferences = requestedReferences ?? [];
            UnpinImported = unpinImported;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected NodePinsImport() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        // Properties.
        public virtual DateTime? CompletedDateTime { get; protected set; }

        public virtual long FailedPins { get; protected set; }

        /// <summary>
        /// The reason the import stopped before its end: null while it runs, and when it completed
        /// </summary>
        public virtual string? FailureReason { get; protected set; }

        public virtual long ImportedPins { get; protected set; }
        public virtual bool IsCompleted => CompletedDateTime.HasValue;

        /// <summary>
        /// The hangfire job running the import
        /// </summary>
        public virtual string? JobId { get; protected set; }

        /// <summary>
        /// Whether the import takes every pin of the node, or the requested references only
        /// </summary>
        public virtual bool ImportsEveryPin => requestedReferences.Count == 0;

        public virtual string NodeId { get; protected set; }

        /// <summary>
        /// The contents to import: empty when the import takes every pin of the node
        /// </summary>
        public virtual IEnumerable<SwarmReference> RequestedReferences
        {
            get => requestedReferences;
            protected set => requestedReferences = new HashSet<SwarmReference>(value ?? []);
        }

        public virtual long SkippedPins { get; protected set; }
        public virtual DateTime? StartedDateTime { get; protected set; }
        public virtual NodePinsImportState State =>
            CompletedDateTime.HasValue ? //an import can fail before starting, when its job is gone
                FailureReason is null ? NodePinsImportState.Completed : NodePinsImportState.Failed :
            StartedDateTime.HasValue ? NodePinsImportState.Running :
            NodePinsImportState.Pending;

        /// <summary>
        /// The pins listed on the node: null until listed
        /// </summary>
        public virtual long? TotalPins { get; protected set; }

        public virtual bool UnpinImported { get; protected set; }
        public virtual long UnpinnedPins { get; protected set; }

        // Methods.
        public virtual void Complete()
        {
            if (IsCompleted)
                throw new InvalidOperationException("Import already completed");

            CompletedDateTime = DateTime.UtcNow;
        }

        public virtual void Fail(string failureReason)
        {
            ArgumentNullException.ThrowIfNull(failureReason);
            if (IsCompleted)
                throw new InvalidOperationException("Import already completed");

            CompletedDateTime = DateTime.UtcNow;
            FailureReason = failureReason;
        }

        public virtual void PinFailed() => FailedPins++;

        public virtual void PinImported() => ImportedPins++;

        public virtual void PinSkipped() => SkippedPins++;

        public virtual void PinUnpinned() => UnpinnedPins++;

        public virtual void SetJobId(string jobId)
        {
            ArgumentNullException.ThrowIfNull(jobId);

            JobId = jobId;
        }

        public virtual void SetTotalPins(long totalPins)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(totalPins);

            TotalPins = totalPins;
        }

        /// <summary>
        /// Start the import, or start it again when its job was interrupted: the counters restart from zero,
        /// the pins already imported are skipped by the import itself
        /// </summary>
        public virtual void Start()
        {
            if (IsCompleted)
                throw new InvalidOperationException("Import already completed");

            FailedPins = 0;
            ImportedPins = 0;
            SkippedPins = 0;
            StartedDateTime = DateTime.UtcNow;
            TotalPins = null;
            UnpinnedPins = 0;
        }
    }
}
