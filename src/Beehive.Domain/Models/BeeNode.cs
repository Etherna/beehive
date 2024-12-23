﻿// Copyright 2021-present Etherna SA
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

using System;

namespace Etherna.Beehive.Domain.Models
{
    public class BeeNode : EntityModelBase<string>
    {
        // Constructors.
        public BeeNode(
            Uri connectionString,
            bool enableBatchCreation)
        {
            ConnectionString = connectionString;
            IsBatchCreationEnabled = enableBatchCreation;
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.
        protected BeeNode() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider declaring as nullable.

        // Properties.
        public virtual Uri ConnectionString { get; set; }
        public virtual bool IsBatchCreationEnabled { get; set; }
    }
}
