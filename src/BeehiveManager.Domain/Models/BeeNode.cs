// Copyright 2021-present Etherna SA
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//     http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using System;

namespace Etherna.BeehiveManager.Domain.Models
{
    public class BeeNode : EntityModelBase<string>
    {
        // Constructors.
        public BeeNode(
            string connectionScheme,
            int gatewayPort,
            string hostname,
            bool enableBatchCreation)
        {
            if (gatewayPort is < 1 or > 65535)
                throw new ArgumentOutOfRangeException(nameof(gatewayPort), "Gateway port is not a valid port");

            ConnectionScheme = connectionScheme;
            GatewayPort = gatewayPort;
            Hostname = hostname;
            IsBatchCreationEnabled = enableBatchCreation;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected BeeNode() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        // Properties.
        public virtual Uri BaseUrl => new($"{ConnectionScheme}://{Hostname}");
        public virtual string ConnectionScheme { get; set; }
        public virtual int GatewayPort { get; set; }
        public virtual Uri GatewayUrl => new($"{ConnectionScheme}://{Hostname}:{GatewayPort}");
        public virtual string Hostname { get; set; }
        public virtual bool IsBatchCreationEnabled { get; set; }
    }
}
