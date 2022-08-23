using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Etherna.BeehiveManager.Services.Extensions
{
    public static class IEnumerableExtensions
    {
        public static IAsyncEnumerable<TSource> Where<TSource>(
            this IEnumerable<TSource> source,
            Func<TSource, Task<bool>> predicate)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (predicate == null)
                throw new ArgumentNullException(nameof(predicate));

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
