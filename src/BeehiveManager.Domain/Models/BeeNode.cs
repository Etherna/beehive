using Etherna.MongODM.Core.Attributes;
using Nethereum.Util;
using System;

namespace Etherna.BeehiveManager.Domain.Models
{
    public class BeeNode : EntityModelBase<string>
    {
        // Constructors.
        public BeeNode(
            Uri url,
            int? gatewayPort,
            int? debugPort)
        {
            DebugPort = debugPort;
            GatewayPort = gatewayPort;
            Url = url;
        }
        protected BeeNode() { }

        // Properties.
        public virtual int? DebugPort { get; set; }
        public virtual string? EthAddress { get; protected set; }
        public virtual int? GatewayPort { get; set; }
        public virtual DateTime? LastInfoRefreshDateTime { get; protected set; }
        public virtual Uri Url { get; set; } = default!;

        // Methods.
        [PropertyAlterer(nameof(EthAddress))]
        [PropertyAlterer(nameof(LastInfoRefreshDateTime))]
        public virtual void SetInfoFromNodeInstance(string ethAddress)
        {
            SetEthAddress(ethAddress);
            LastInfoRefreshDateTime = DateTime.Now;
        }

        // Helpers.
        private void SetEthAddress(string address)
        {
            if (!address.IsValidEthereumAddressHexFormat())
                throw new ArgumentException("The value is not a valid address", nameof(address));

            EthAddress = address.ConvertToEthereumChecksumAddress();
        }
    }
}
