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

using Etherna.MongoDB.Driver;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Helpers
{
    /// <summary>
    /// An in-memory queryable the driver's async materializers accept (ToListAsync and the others need an
    /// IAsyncCursorSource), so a repository query lambda can run as is over test data
    /// </summary>
    internal sealed class InMemoryQueryable<T>(IQueryable<T> source) : IOrderedQueryable<T>, IAsyncCursorSource<T>
    {
        // Properties.
        public Type ElementType => source.ElementType;
        public Expression Expression => source.Expression;
        public IQueryProvider Provider { get; } = new InMemoryQueryProvider(source.Provider);

        // Methods.
        public IEnumerator<T> GetEnumerator() => source.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        public IAsyncCursor<T> ToCursor(CancellationToken cancellationToken = default) =>
            new InMemoryCursor([.. source]);
        public Task<IAsyncCursor<T>> ToCursorAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(ToCursor(cancellationToken));

        // Helpers.
        private sealed class InMemoryCursor(List<T> items) : IAsyncCursor<T>
        {
            private bool consumed;

            public IEnumerable<T> Current => items;
            public void Dispose() { }
            public bool MoveNext(CancellationToken cancellationToken = default)
            {
                if (consumed)
                    return false;
                consumed = true;
                return true;
            }
            public Task<bool> MoveNextAsync(CancellationToken cancellationToken = default) =>
                Task.FromResult(MoveNext(cancellationToken));
        }

        private sealed class InMemoryQueryProvider(IQueryProvider source) : IQueryProvider
        {
            public IQueryable CreateQuery(Expression expression) =>
                throw new NotSupportedException();
            public IQueryable<TElement> CreateQuery<TElement>(Expression expression) =>
                new InMemoryQueryable<TElement>(source.CreateQuery<TElement>(expression));
            public object? Execute(Expression expression) => source.Execute(expression);
            public TResult Execute<TResult>(Expression expression) => source.Execute<TResult>(expression);
        }
    }

    internal static class InMemoryQueryable
    {
        public static IQueryable<T> Of<T>(IEnumerable<T> items) =>
            new InMemoryQueryable<T>(items.AsQueryable());
    }
}
