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
using Etherna.MongODM.Core.Attributes;
using System;
using System.Collections.Generic;

namespace Etherna.Beehive.Domain.Models
{
    public class ChunkPin : EntityModelBase<string>
    {
        // Consts.
#pragma warning disable CA1051
        public readonly TimeSpan TemporaryPinTtl = TimeSpan.FromHours(24);
#pragma warning restore CA1051
        
        // Fields.
        private List<SwarmHash> missingChunks = [];
        
        // Constructors.
        /// <summary>
        /// Create a new chunk pin
        /// </summary>
        /// <param name="hash">
        /// The root pinning hash.
        /// Set to null to have a temporary pin, when the root hash isn't known upfront.
        /// Consider <see cref="TemporaryPinTtl"/> as TTL for a temporary pin.
        /// </param>
        public ChunkPin(SwarmHash? hash)
        {
            Hash = hash;
            IsProcessed = false;
            IsSucceeded = false;
        }
        protected ChunkPin() { }

        // Properties.
        public virtual IEnumerable<SwarmHash> MissingChunks
        {
            get => missingChunks;
            set => missingChunks = new List<SwarmHash>(value ?? []);
        }
        public virtual SwarmHash? Hash { get; protected set; }
        public virtual bool IsAlive => Hash.HasValue ||
                                       CreationDateTime + TemporaryPinTtl >= DateTime.UtcNow;
        public virtual bool IsProcessed { get; protected set; }
        public virtual bool IsSucceeded { get; protected set; }
        public virtual long TotPinnedChunks { get; protected set; }
        
        // Methods.
        [PropertyAlterer(nameof(Hash))]
        public virtual void SetChunkReference(SwarmChunkReference reference)
        {
            ArgumentNullException.ThrowIfNull(reference, nameof(reference));
            
            if (Hash.HasValue)
                throw new InvalidOperationException();
            Hash = reference.Hash;
        }
    }
}