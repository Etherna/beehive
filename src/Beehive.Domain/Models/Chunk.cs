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
    public class Chunk : EntityModelBase<string>
    {
        // Fields.
        private List<ChunkPin> _pins = [];
        
        // Constructors.
        public Chunk(
            SwarmHash hash,
            ReadOnlyMemory<byte> payload,
            bool isSoc)
        {
            Hash = hash;
            IsSoc = isSoc;
            Payload = payload;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected Chunk() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        // Properties.
        public virtual SwarmHash Hash { get; protected set; }
        public virtual bool IsSoc { get; protected set; }
        public virtual ReadOnlyMemory<byte> Payload { get; protected set; }
        public virtual IEnumerable<ChunkPin> Pins
        {
            get => _pins;
            protected set => _pins = new List<ChunkPin>(value ?? []);
        }

        // Methods.
        [PropertyAlterer(nameof(Pins))]
        public void AddPin(ChunkPin chunkPin)
        {
            _pins.Add(chunkPin);
        }
    }
}