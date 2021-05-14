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
