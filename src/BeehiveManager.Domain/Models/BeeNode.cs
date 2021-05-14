using Etherna.MongODM.Core.Attributes;
using Nethereum.Util;
using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Etherna.BeehiveManager.Domain.Models
{
    public class BeeNode : EntityModelBase<string>
    {
        // Constructors.
        public BeeNode(
            int? debugPort,
            int? gatewayPort,
            string url)
        {
            if (debugPort is not null and (< 1 or > 65535))
                throw new ArgumentOutOfRangeException(nameof(debugPort), "Debug port is not a valid port");
            if (gatewayPort is not null and (< 1 or > 65535))
                throw new ArgumentOutOfRangeException(nameof(gatewayPort), "Gateway port is not a valid port");
            if (gatewayPort is not null && gatewayPort == debugPort)
                throw new ArgumentException("Gateway and debug ports can't be the same");
            if (url is null)
                throw new ArgumentNullException(nameof(url));

            DebugPort = debugPort;
            GatewayPort = gatewayPort;
            Url = NormalizeUrl(url);
        }
        protected BeeNode() { }

        // Properties.
        public virtual int? DebugPort { get; set; }
        public virtual string? EthAddress { get; protected set; }
        public virtual int? GatewayPort { get; set; }
        public virtual DateTime? LastRefreshDateTime { get; protected set; }
        public virtual Uri Url { get; protected set; } = default!;

        // Methods.
        [PropertyAlterer(nameof(EthAddress))]
        [PropertyAlterer(nameof(LastRefreshDateTime))]
        public virtual void SetInfoFromNodeInstance(string ethAddress)
        {
            if (!ethAddress.IsValidEthereumAddressHexFormat())
                throw new ArgumentException("The value is not a valid address", nameof(ethAddress));

            EthAddress = ethAddress.ConvertToEthereumChecksumAddress();
            LastRefreshDateTime = DateTime.Now;
        }

        [PropertyAlterer(nameof(Url))]
        public virtual void SetUrl(string url)
        {
            if (url is null)
                throw new ArgumentNullException(nameof(url));

            Url = NormalizeUrl(url);
        }

        // Helpers.
        private static Uri NormalizeUrl(string url)
        {
            var normalizedUrl = url;
            if (normalizedUrl.Last() != '/')
                normalizedUrl += '/';

            var urlRegex = new Regex(@"^((?<proto>\w+)://)?[^/]+?(?<port>:\d+)?/(?<path>.*)",
                RegexOptions.None, TimeSpan.FromMilliseconds(150));
            var urlMatch = urlRegex.Match(normalizedUrl);

            if (!urlMatch.Success)
                throw new ArgumentException("Url is not valid", nameof(url));

            if (string.IsNullOrEmpty(urlMatch.Groups["proto"].Value))
                normalizedUrl = $"{Uri.UriSchemeHttp}://{normalizedUrl}";

            if (!string.IsNullOrEmpty(urlMatch.Groups["path"].Value))
                throw new ArgumentException("Url can't have an internal path or query", nameof(url));

            if (!string.IsNullOrEmpty(urlMatch.Groups["port"].Value))
                throw new ArgumentException("Url can't specify a port", nameof(url));

            return new Uri(normalizedUrl, UriKind.Absolute);
        }
    }
}
