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

namespace Etherna.Beehive.Areas.Api.V0_4.DtoModels
{
    public class BeeNodeDto(
        string id,
        Uri connectionString,
        IEnumerable<string> errors,
        EthAddress? ethereumAddress,
        DateTime heartbeatTimeStamp,
        bool isAlive,
        bool isBatchCreationEnabled,
        SwarmOverlayAddress? overlayAddress,
        string? pssPublicKey,
        string? publicKey)
    {
        public string Id { get; } = id;
        public Uri ConnectionString { get; } = connectionString;
        public IEnumerable<string> Errors { get; } = errors;
        public EthAddress? EthereumAddress { get; } = ethereumAddress;
        public DateTime HeartbeatTimeStamp { get; } = heartbeatTimeStamp;
        public bool IsAlive { get; } = isAlive;
        public bool IsBatchCreationEnabled { get; } = isBatchCreationEnabled;
        public SwarmOverlayAddress? OverlayAddress { get; } = overlayAddress;
        public string? PssPublicKey { get; } = pssPublicKey;
        public string? PublicKey { get; } = publicKey;
    }
}