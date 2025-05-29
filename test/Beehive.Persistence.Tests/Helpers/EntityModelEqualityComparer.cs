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

using Etherna.MongODM.Core.Domain.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Etherna.Beehive.Persistence.Helpers
{
    internal sealed class EntityModelEqualityComparer : IEqualityComparer<IEntityModel<string>?>
    {
        public static EntityModelEqualityComparer Instance { get; } = new EntityModelEqualityComparer();

        public bool Equals(IEntityModel<string>? x, IEntityModel<string>? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x is null) return false;
            if (y is null) return false;
            return x.Id == y.Id;
        }

        public int GetHashCode([DisallowNull] IEntityModel<string>? obj)
        {
            if (obj?.Id is null)
                return -1;
            return obj.Id.GetHashCode(StringComparison.Ordinal);
        }
    }
}