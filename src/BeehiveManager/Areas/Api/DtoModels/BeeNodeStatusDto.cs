﻿// Copyright 2021-present Etherna SA
// This file is part of BeehiveManager.
// 
// BeehiveManager is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeehiveManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeehiveManager.
// If not, see <https://www.gnu.org/licenses/>.

using Etherna.BeehiveManager.Services.Utilities.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public class BeeNodeStatusDto
    {
        // Constructor.
        public BeeNodeStatusDto(string id, BeeNodeStatus status)
        {
            ArgumentNullException.ThrowIfNull(status, nameof(status));

            Id = id;
            Errors = status.Errors;
            EthereumAddress = status.Addresses?.Ethereum;
            HeartbeatTimeStamp = status.HeartbeatTimeStamp;
            IsAlive = status.IsAlive;
            OverlayAddress = status.Addresses?.Overlay;
            PinnedHashes = status.PinnedHashes.Select(h => h.ToString());
            PostageBatchesId = status.PostageBatchesId.Select(b => b.ToString());
            PssPublicKey = status.Addresses?.PssPublicKey;
            PublicKey = status.Addresses?.PublicKey;
        }

        // Properties.
        public string Id { get; }
        public IEnumerable<string> Errors { get; }
        public string? EthereumAddress { get; }
        public DateTime HeartbeatTimeStamp { get; }
        public bool IsAlive { get; }
        public string? OverlayAddress { get; }
        public IEnumerable<string> PinnedHashes { get; }
        public IEnumerable<string> PostageBatchesId { get; }
        public string? PssPublicKey { get; }
        public string? PublicKey { get; }
    }
}
