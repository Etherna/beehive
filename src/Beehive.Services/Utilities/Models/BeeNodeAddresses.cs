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

namespace Etherna.Beehive.Services.Utilities.Models
{
    public class BeeNodeAddresses(
        EthAddress ethereum,
        string overlay,
        string pssPublicKey,
        string publicKey)
    {
        // Properties.
        public EthAddress Ethereum { get; } = ethereum;
        public string Overlay { get; } = overlay ?? throw new ArgumentNullException(nameof(overlay));
        public string PssPublicKey { get; } = pssPublicKey ?? throw new ArgumentNullException(nameof(pssPublicKey));
        public string PublicKey { get; } = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
    }
}
