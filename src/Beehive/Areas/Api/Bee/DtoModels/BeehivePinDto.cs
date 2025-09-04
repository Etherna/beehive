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

using Etherna.BeeNet.Models;
using System;
using System.Collections.Generic;

namespace Etherna.Beehive.Areas.Api.Bee.DtoModels
{
    public sealed class BeehivePinDto(
        SwarmReference reference,
        DateTimeOffset creationTime,
        IEnumerable<SwarmHash> missingChunks,
        bool processed,
        bool succeeded,
        long pinnedChunks)
    {
        public SwarmReference Reference { get; } = reference;
        public DateTimeOffset CreationTime { get; } = creationTime;
        public bool Processed { get; } = processed;
        public bool Succeeded { get; } = succeeded;
        public IEnumerable<SwarmHash> MissingChunks { get; } = missingChunks;
        public long PinnedChunks { get; } = pinnedChunks;
    }
}