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

namespace Etherna.Beehive.Domain.Models
{
    /// <summary>
    /// The check of one pinned content, within a <see cref="PinsCheck"/>: the chunks of the content, the ones
    /// beehive doesn't hold and the invalid ones, or why the content could not be checked
    /// </summary>
    public class PinCheck : EntityModelBase<string>
    {
        // Constructors.
        public PinCheck(PinsCheck parentCheck, SwarmReference reference)
        {
            ArgumentNullException.ThrowIfNull(parentCheck);

            ParentCheck = parentCheck;
            Reference = reference;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected PinCheck() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        // Properties.
        /// <summary>
        /// Why the content could not be checked: null when it was
        /// </summary>
        public virtual string? FailureReason { get; protected set; }

        /// <summary>
        /// The chunks of the content whose payload is not the chunk at their address, or not a node of its tree
        /// </summary>
        public virtual long InvalidChunks { get; protected set; }

        /// <summary>
        /// The chunks of the content beehive doesn't hold
        /// </summary>
        public virtual long MissingChunks { get; protected set; }

        public virtual PinCheckOutcome Outcome =>
            FailureReason is not null ? PinCheckOutcome.Failed :
            InvalidChunks > 0 ? PinCheckOutcome.Invalid :
            MissingChunks > 0 ? PinCheckOutcome.Incomplete :
            PinCheckOutcome.Complete;

        /// <summary>
        /// The run this record belongs to
        /// </summary>
        public virtual PinsCheck ParentCheck { get; protected set; }

        public virtual SwarmReference Reference { get; protected set; }

        /// <summary>
        /// The chunks of the content: the ones its check met, found or not
        /// </summary>
        public virtual long TotalChunks { get; protected set; }

        // Methods.
        public virtual void SetChecked(long totalChunks, long missingChunks, long invalidChunks)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(totalChunks);
            ArgumentOutOfRangeException.ThrowIfNegative(missingChunks);
            ArgumentOutOfRangeException.ThrowIfNegative(invalidChunks);

            InvalidChunks = invalidChunks;
            MissingChunks = missingChunks;
            TotalChunks = totalChunks;
        }

        public virtual void SetFailed(string failureReason)
        {
            ArgumentNullException.ThrowIfNull(failureReason);

            FailureReason = failureReason;
        }
    }
}
