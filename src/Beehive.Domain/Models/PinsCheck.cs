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
    /// A run of the integrity check of beehive's own pins, as a background job: over every pin, over the pins of the
    /// requested states, or over the requested references only. Every pin is corrected to what the run finds
    /// </summary>
    public class PinsCheck : EntityModelBase<string>
    {
        // Fields.
        private HashSet<ChunkPinState> requestedPinStates = [];
        private HashSet<SwarmReference> requestedReferences = [];

        // Constructors.
        /// <param name="requestedPinStates">The states of the pins to check, when no reference is requested: null or empty for the pins of every state</param>
        /// <param name="requestedReferences">The contents to check: null or empty for the pins chosen by their state</param>
        public PinsCheck(
            IEnumerable<ChunkPinState>? requestedPinStates = null,
            IEnumerable<SwarmReference>? requestedReferences = null)
        {
            RequestedPinStates = requestedPinStates ?? [];
            RequestedReferences = requestedReferences ?? [];
            if (this.requestedPinStates.Count > 0 && this.requestedReferences.Count > 0)
                throw new ArgumentException("A check takes the requested references, or the pins of the requested states: not both");
            foreach (var pinState in this.requestedPinStates)
                if (!Enum.IsDefined(pinState))
                    throw new ArgumentException($"{pinState} is not the state of a pin");
        }
        protected PinsCheck() { }

        // Properties.
        /// <summary>
        /// The pins the run got an outcome for
        /// </summary>
        public virtual long CheckedPins => CompletePins + FailedPins + IncompletePins + InvalidPins;

        /// <summary>
        /// Whether the run takes the requested references, or the pins chosen by their state
        /// </summary>
        public virtual bool ChecksRequestedReferences => requestedReferences.Count > 0;

        public virtual DateTime? CompletedDateTime { get; protected set; }

        /// <summary>
        /// The pins beehive holds in full
        /// </summary>
        public virtual long CompletePins { get; protected set; }

        /// <summary>
        /// The pins that could not be checked
        /// </summary>
        public virtual long FailedPins { get; protected set; }

        /// <summary>
        /// The reason the run stopped before its end: null while it runs, when it completed, and when it was stopped
        /// </summary>
        public virtual string? FailureReason { get; protected set; }

        /// <summary>
        /// The pins with chunks beehive doesn't hold, and no invalid one
        /// </summary>
        public virtual long IncompletePins { get; protected set; }

        /// <summary>
        /// The pins with invalid chunks
        /// </summary>
        public virtual long InvalidPins { get; protected set; }

        public virtual bool IsCompleted => CompletedDateTime.HasValue;

        /// <summary>
        /// Whether the run ended on a stop request, before checking everything
        /// </summary>
        public virtual bool IsStopped { get; protected set; }

        /// <summary>
        /// The hangfire job running the check
        /// </summary>
        public virtual string? JobId { get; protected set; }

        /// <summary>
        /// The states of the pins the run takes, when no reference is requested: empty for the pins of every state
        /// </summary>
        public virtual IEnumerable<ChunkPinState> RequestedPinStates
        {
            get => requestedPinStates;
            protected set => requestedPinStates = new HashSet<ChunkPinState>(value ?? []);
        }

        /// <summary>
        /// The contents to check: empty when the run takes the pins chosen by their state
        /// </summary>
        public virtual IEnumerable<SwarmReference> RequestedReferences
        {
            get => requestedReferences;
            protected set => requestedReferences = new HashSet<SwarmReference>(value ?? []);
        }

        /// <summary>
        /// The content the run is checking: null when it isn't running
        /// </summary>
        public virtual SwarmReference? RunningReference { get; protected set; }

        public virtual DateTime? StartedDateTime { get; protected set; }
        public virtual PinsCheckState State =>
            CompletedDateTime.HasValue ? //a run can fail before starting, when its job is gone
                IsStopped ? PinsCheckState.Stopped :
                FailureReason is null ? PinsCheckState.Completed :
                PinsCheckState.Failed :
            StartedDateTime.HasValue ? PinsCheckState.Running :
            PinsCheckState.Pending;

        /// <summary>
        /// When a stop was asked: the run stops at the end of the pin it is checking
        /// </summary>
        public virtual DateTime? StopRequestedDateTime { get; protected set; }

        /// <summary>
        /// The pins to check: null until listed
        /// </summary>
        public virtual long? TotalPins { get; protected set; }

        // Methods.
        public virtual void Complete()
        {
            if (IsCompleted)
                throw new InvalidOperationException("Check already completed");

            CompletedDateTime = DateTime.UtcNow;
            RunningReference = null;
        }

        public virtual void Fail(string failureReason)
        {
            ArgumentNullException.ThrowIfNull(failureReason);
            if (IsCompleted)
                throw new InvalidOperationException("Check already completed");

            CompletedDateTime = DateTime.UtcNow;
            FailureReason = failureReason;
            RunningReference = null;
        }

        /// <summary>
        /// Count a pin by the outcome of its check
        /// </summary>
        public virtual void PinChecked(PinCheckOutcome outcome)
        {
            switch (outcome)
            {
                case PinCheckOutcome.Complete: CompletePins++; break;
                case PinCheckOutcome.Failed: FailedPins++; break;
                case PinCheckOutcome.Incomplete: IncompletePins++; break;
                case PinCheckOutcome.Invalid: InvalidPins++; break;
                default: throw new ArgumentOutOfRangeException(nameof(outcome));
            }
        }

        public virtual void RequestStop()
        {
            if (IsCompleted)
                throw new InvalidOperationException("Check already completed");

            StopRequestedDateTime ??= DateTime.UtcNow;
        }

        public virtual void SetJobId(string jobId)
        {
            ArgumentNullException.ThrowIfNull(jobId);

            JobId = jobId;
        }

        public virtual void SetRunningReference(SwarmReference reference) =>
            RunningReference = reference;

        /// <summary>
        /// Start the run, or start it again when its job was interrupted: a run started again goes on from the pins
        /// it recorded, and its counters are their outcomes
        /// </summary>
        /// <param name="recordedOutcomes">The outcomes of the pins the run recorded already</param>
        public virtual void Start(long totalPins, IEnumerable<PinCheckOutcome> recordedOutcomes)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(totalPins);
            ArgumentNullException.ThrowIfNull(recordedOutcomes);
            if (IsCompleted)
                throw new InvalidOperationException("Check already completed");

            CompletePins = 0;
            FailedPins = 0;
            IncompletePins = 0;
            InvalidPins = 0;
            foreach (var outcome in recordedOutcomes)
                PinChecked(outcome);
            StartedDateTime ??= DateTime.UtcNow;
            TotalPins = totalPins;
        }

        public virtual void StopOnRequest()
        {
            if (IsCompleted)
                throw new InvalidOperationException("Check already completed");

            CompletedDateTime = DateTime.UtcNow;
            IsStopped = true;
            RunningReference = null;
        }
    }
}
