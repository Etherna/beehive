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

using Etherna.BeehiveManager.Domain.Models;
using System;

namespace Etherna.BeehiveManager.Areas.Api.DtoModels
{
    public class BeeNodeDto
    {
        // Constructors.
        public BeeNodeDto(BeeNode beeNode)
        {
            ArgumentNullException.ThrowIfNull(beeNode, nameof(beeNode));

            Id = beeNode.Id;
            ConnectionScheme = beeNode.ConnectionScheme;
            GatewayPort = beeNode.GatewayPort;
            Hostname = beeNode.Hostname;
            IsBatchCreationEnabled = beeNode.IsBatchCreationEnabled;
        }

        // Properties.
        public string Id { get; }
        public string ConnectionScheme { get; }
        public int GatewayPort { get; }
        public string Hostname { get; }
        public bool IsBatchCreationEnabled { get; }
    }
}
