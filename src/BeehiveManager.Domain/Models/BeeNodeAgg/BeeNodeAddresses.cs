//   Copyright 2021-present Etherna Sagl
//
//   Licensed under the Apache License, Version 2.0 (the "License");
//   you may not use this file except in compliance with the License.
//   You may obtain a copy of the License at
//
//       http://www.apache.org/licenses/LICENSE-2.0
//
//   Unless required by applicable law or agreed to in writing, software
//   distributed under the License is distributed on an "AS IS" BASIS,
//   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//   See the License for the specific language governing permissions and
//   limitations under the License.

using Nethereum.Util;
using System;

namespace Etherna.BeehiveManager.Domain.Models.BeeNodeAgg
{
    public class BeeNodeAddresses : ModelBase
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
        protected BeeNodeAddresses() { }

        // Properties.
        public virtual string Ethereum { get; protected set; } = default!;
        public virtual string Overlay { get; protected set; } = default!;
        public virtual string PssPublicKey { get; protected set; } = default!;
        public virtual string PublicKey { get; protected set; } = default!;
    }
}
