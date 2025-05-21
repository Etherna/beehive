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
        public ChunkPin(SwarmChunkReference? chunkReference)
        {
            EncryptionKey = chunkReference?.EncryptionKey;
            Hash = chunkReference?.Hash;
            IsProcessed = false;
            RecursiveEncryption = chunkReference?.UseRecursiveEncryption ?? false;
        }
        protected ChunkPin() { }

        // Properties.
        public virtual XorEncryptKey? EncryptionKey { get; protected set; }
        public virtual SwarmHash? Hash { get; protected set; }
        public virtual bool IsExpired =>
            !Hash.HasValue &&
            CreationDateTime + provisionalTtl < DateTime.UtcNow;
        public virtual bool IsProcessed { get; protected set; }
        public virtual bool IsSucceeded => IsProcessed && missingChunks.Count == 0;
        public virtual IEnumerable<SwarmHash> MissingChunks
        {
            get => missingChunks;
            protected set => missingChunks = new HashSet<SwarmHash>(value ?? []);
        }
        public virtual bool RecursiveEncryption { get; protected set; }
        public virtual long TotPinnedChunks { get; protected set; }
        
        // Methods.
        [PropertyAlterer(nameof(EncryptionKey))]
        [PropertyAlterer(nameof(Hash))]
        [PropertyAlterer(nameof(IsProcessed))]
        [PropertyAlterer(nameof(RecursiveEncryption))]
        [PropertyAlterer(nameof(TotPinnedChunks))]
        public virtual void SucceededProvisional(
            SwarmChunkReference rootChunkRef,
            long totPinnedChunks)
        {
            ArgumentNullException.ThrowIfNull(rootChunkRef, nameof(rootChunkRef));

            if (Hash.HasValue)
                throw new InvalidOperationException();
            
            EncryptionKey = rootChunkRef.EncryptionKey;
            Hash = rootChunkRef.Hash;
            IsProcessed = true;
            RecursiveEncryption = rootChunkRef.UseRecursiveEncryption;
            TotPinnedChunks = totPinnedChunks;
        }

        [PropertyAlterer(nameof(IsProcessed))]
        [PropertyAlterer(nameof(MissingChunks))]
        [PropertyAlterer(nameof(TotPinnedChunks))]
        public virtual void UpdateProcessed(
            IEnumerable<SwarmHash> missingChunks,
            long totPinnedChunks)
        {
            if (!Hash.HasValue)
                throw new InvalidOperationException("Hash is not set");
            
            IsProcessed = true;
            MissingChunks = missingChunks;
            TotPinnedChunks = totPinnedChunks;
        }
    }
}