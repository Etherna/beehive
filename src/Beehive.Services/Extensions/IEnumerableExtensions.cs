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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Extensions
{
    public static class IEnumerableExtensions
    {
        public static IAsyncEnumerable<TSource> Where<TSource>(
            this IEnumerable<TSource> source,
            Func<TSource, Task<bool>> predicate)
        {
            ArgumentNullException.ThrowIfNull(source, nameof(source));
            ArgumentNullException.ThrowIfNull(predicate, nameof(predicate));

            return WhereImpl(source, predicate);
        }

        // Helpers.
        private static async IAsyncEnumerable<TSource> WhereImpl<TSource>(
            this IEnumerable<TSource> source,
            Func<TSource, Task<bool>> predicate)
        {
            foreach (var item in source)
                if (await predicate(item))
                    yield return item;
        }
    }
}
