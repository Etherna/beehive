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
        private readonly TimeSpan provisionalTtl = TimeSpan.FromHours(24);
        
        // Fields.
        private HashSet<SwarmHash> missingChunks = [];
        
        // Constructors.
        /// <summary>
        /// Create a new chunk pin
        /// </summary>
        /// <param name="hash">
        /// The root pinning hash.
        /// Set to null for a provisional pin, when the chunk reference isn't known upfront.
        /// </param>
        public ChunkPin(SwarmReference? reference)
        {
            Reference = reference;
            IsProcessed = false;
        }
        protected ChunkPin() { }

        // Properties.
        public virtual SwarmReference? Reference { get; protected set; }
        public virtual bool IsExpired =>
            !Reference.HasValue &&
            CreationDateTime + provisionalTtl < DateTime.UtcNow;
        public virtual bool IsProcessed { get; protected set; }
        public virtual bool IsSucceeded => IsProcessed && missingChunks.Count == 0;
        public virtual IEnumerable<SwarmHash> MissingChunks
        {
            get => missingChunks;
            protected set => missingChunks = new HashSet<SwarmHash>(value ?? []);
        }
        public virtual long TotPinnedChunks { get; protected set; }
        
        // Methods.
        [PropertyAlterer(nameof(Reference))]
        [PropertyAlterer(nameof(IsProcessed))]
        [PropertyAlterer(nameof(TotPinnedChunks))]
        public virtual void UploadSucceeded(
            SwarmReference rootChunkRef,
            long totPinnedChunks)
        {
            if (Reference.HasValue)
                throw new InvalidOperationException();
            
            Reference = rootChunkRef;
            IsProcessed = true;
            TotPinnedChunks = totPinnedChunks;
        }

        [PropertyAlterer(nameof(IsProcessed))]
        [PropertyAlterer(nameof(MissingChunks))]
        [PropertyAlterer(nameof(TotPinnedChunks))]
        public virtual void UpdateProcessed(
            IEnumerable<SwarmHash> missingChunks,
            long totPinnedChunks)
        {
            if (!Reference.HasValue)
                throw new InvalidOperationException("Hash is not set");
            
            IsProcessed = true;
            MissingChunks = missingChunks;
            TotPinnedChunks = totPinnedChunks;
        }
    }
}