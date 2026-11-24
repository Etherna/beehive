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

namespace Etherna.Beehive.Areas.Api.DtoModels
{
    /// <summary>
    /// The check of one pinned content within a check of the pins. Declared in reading order: the reference, its
    /// outcome, then the counts of the single check of a pin, the total after its parts
    /// </summary>
    public sealed class PinCheckDto(PinCheck record)
    {
        public string Id { get; } = record.Id;
        public string CheckId { get; } = record.ParentCheck.Id;
        public SwarmReference Reference { get; } = record.Reference;
        public PinCheckOutcome Outcome { get; } = record.Outcome;
        public long Missing { get; } = record.MissingChunks;
        public long Invalid { get; } = record.InvalidChunks;
        public long Total { get; } = record.TotalChunks;

        /// <summary>
        /// Why the content could not be checked: null when it was
        /// </summary>
        public string? FailureReason { get; } = record.FailureReason;

        public DateTime CreationDateTime { get; } = record.CreationDateTime;
    }
}
