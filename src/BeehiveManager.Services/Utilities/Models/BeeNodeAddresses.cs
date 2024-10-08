// Copyright 2021-present Etherna SA
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

using Nethereum.Util;
using System;

namespace Etherna.BeehiveManager.Services.Utilities.Models
{
    public class BeeNodeAddresses
    {
        // Constructors.
        public BeeNodeAddresses(
            string ethereum,
            string overlay,
            string pssPublicKey,
            string publicKey)
        {
            if (!ethereum.IsValidEthereumAddressHexFormat())
                throw new ArgumentException("The value is not a valid address", nameof(ethereum));

            Ethereum = ethereum.ConvertToEthereumChecksumAddress();
            Overlay = overlay ?? throw new ArgumentNullException(nameof(overlay));
            PssPublicKey = pssPublicKey ?? throw new ArgumentNullException(nameof(pssPublicKey));
            PublicKey = publicKey ?? throw new ArgumentNullException(nameof(publicKey));
        }

        // Properties.
        public string Ethereum { get; }
        public string Overlay { get; }
        public string PssPublicKey { get; }
        public string PublicKey { get; }
    }
}
