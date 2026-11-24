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
    /// <summary>
    /// A check of beehive's pins run as a background job. Declared in reading order: what it checks, its pins by
    /// outcome with their total after its parts, then its job and its times
    /// </summary>
    public sealed class PinsCheckDto(PinsCheck check)
    {
        public string Id { get; } = check.Id;
        public PinsCheckState State { get; } = check.State;

        /// <summary>
        /// The states of the pins the check takes, when no reference was requested: empty for the pins of every
        /// state
        /// </summary>
        public IEnumerable<ChunkPinState> RequestedPinStates { get; } = check.RequestedPinStates;

        /// <summary>
        /// The contents the check was asked for: empty when it takes the pins chosen by their state
        /// </summary>
        public IEnumerable<SwarmReference> RequestedReferences { get; } = check.RequestedReferences;

        /// <summary>
        /// The content the check is on: null when it isn't running
        /// </summary>
        public SwarmReference? RunningReference { get; } = check.RunningReference;

        /// <summary>
        /// The pins beehive holds in full
        /// </summary>
        public long CompletePins { get; } = check.CompletePins;

        /// <summary>
        /// The pins with chunks beehive doesn't hold, and no invalid one
        /// </summary>
        public long IncompletePins { get; } = check.IncompletePins;

        /// <summary>
        /// The pins with invalid chunks
        /// </summary>
        public long InvalidPins { get; } = check.InvalidPins;

        /// <summary>
        /// The pins that could not be checked
        /// </summary>
        public long FailedPins { get; } = check.FailedPins;

        /// <summary>
        /// The pins the check got an outcome for: complete, incomplete, invalid or failed
        /// </summary>
        public long CheckedPins { get; } = check.CheckedPins;

        /// <summary>
        /// The pins to check: null until listed
        /// </summary>
        public long? TotalPins { get; } = check.TotalPins;

        public string? Error { get; } = check.FailureReason;
        public string? JobId { get; } = check.JobId;
        public DateTime CreationDateTime { get; } = check.CreationDateTime;
        public DateTime? StartedDateTime { get; } = check.StartedDateTime;
        public DateTime? StopRequestedDateTime { get; } = check.StopRequestedDateTime;
        public DateTime? CompletedDateTime { get; } = check.CompletedDateTime;
    }
}
