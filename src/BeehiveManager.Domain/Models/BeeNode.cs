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

using Etherna.BeehiveManager.Domain.Events;
using Etherna.BeehiveManager.Domain.Models.BeeNodeAgg;
using Etherna.MongODM.Core.Attributes;
using System;

namespace Etherna.BeehiveManager.Domain.Models
{
    public class BeeNode : EntityModelBase<string>
    {
        // Constructors.
        public BeeNode(
            string connectionScheme,
            int debugPort,
            int gatewayPort,
            string hostname)
        {
            if (debugPort is < 1 or > 65535)
                throw new ArgumentOutOfRangeException(nameof(debugPort), "Debug port is not a valid port");
            if (gatewayPort is < 1 or > 65535)
                throw new ArgumentOutOfRangeException(nameof(gatewayPort), "Gateway port is not a valid port");
            if (gatewayPort == debugPort)
                throw new ArgumentException("Gateway and debug ports can't be the same");

            ConnectionScheme = connectionScheme;
            DebugPort = debugPort;
            GatewayPort = gatewayPort;
            Hostname = hostname;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected BeeNode() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        // Properties.
        public virtual BeeNodeAddresses? Addresses { get; protected set; }
        public virtual string ConnectionScheme { get; set; }
        public virtual Uri BaseUrl => new($"{ConnectionScheme}://{Hostname}");
        public virtual int DebugPort { get; set; }
        public virtual Uri DebugUrl => new($"{ConnectionScheme}://{Hostname}:{DebugPort}");
        public virtual int GatewayPort { get; set; }
        public virtual Uri GatewayUrl => new($"{ConnectionScheme}://{Hostname}:{GatewayPort}");
        public virtual string Hostname { get; set; }

        // Methods.
        [PropertyAlterer(nameof(Addresses))]
        public virtual void SetAddresses(BeeNodeAddresses addresses)
        {
            if (Addresses is not null)
                throw new InvalidOperationException("Addresses already set");

            Addresses = addresses ?? throw new ArgumentNullException(nameof(addresses));

            AddEvent(new SetBeeNodeAddressesEvent(this));
        }
    }
}
