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

using Etherna.Beehive.Domain;
using Etherna.Beehive.Domain.Models;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.GridFS;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.ExecContext.AsyncLocal;
using Etherna.Scrinium.Core.Repositories;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Helpers
{
    /// <summary>
    /// A db context mock over in-memory lists, for the flows reading and writing chunks and pins: the repository
    /// lambdas run as they are over the lists, the GridFS bucket holds nothing, the engine opens its execution
    /// context on demand, and a save of the changes does nothing
    /// </summary>
    internal sealed class InMemoryBeehiveDb
    {
        // Constructor.
        public InMemoryBeehiveDb()
        {
            // Chunks: lookups, queries, creations and the add-to-set of a pin over the list.
            ChunksMock.Setup(r => r.TryFindOneAsync(
                    It.IsAny<Expression<Func<Chunk, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<Chunk, bool>> predicate, CancellationToken _) =>
                    Chunks.FirstOrDefault(predicate.Compile()));
            ChunksMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<Chunk>, Task<List<Chunk>>>>(),
                    null))
                .Returns((Func<IQueryable<Chunk>, Task<List<Chunk>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(Chunks)));
            ChunksMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<Chunk>, Task<List<SwarmHash>>>>(),
                    null))
                .Returns((Func<IQueryable<Chunk>, Task<List<SwarmHash>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(Chunks)));
            ChunksMock.Setup(r => r.CreateAsync(It.IsAny<Chunk>(), It.IsAny<CancellationToken>()))
                .Callback((Chunk chunk, CancellationToken _) => AddChunk(chunk))
                .Returns(Task.CompletedTask);
            ChunksMock.Setup(r => r.CreateAsync(It.IsAny<IEnumerable<Chunk>>(), It.IsAny<CancellationToken>()))
                .Callback((IEnumerable<Chunk> chunks, CancellationToken _) =>
                {
                    foreach (var chunk in chunks.ToArray())
                        AddChunk(chunk);
                })
                .Returns(Task.CompletedTask);
            ChunksMock.Setup(r => r.TryFindOneAndAddToSetAsync(
                    It.IsAny<FilterDefinition<Chunk>>(),
                    It.IsAny<Expression<Func<Chunk, IEnumerable<ChunkPin>>>>(),
                    It.IsAny<ChunkPin>(),
                    It.IsAny<FindOneAndUpdateOptions<Chunk>>(),
                    It.IsAny<CancellationToken>()))
                .Returns((
                    FilterDefinition<Chunk> filter,
                    Expression<Func<Chunk, IEnumerable<ChunkPin>>> _,
                    ChunkPin pin,
                    FindOneAndUpdateOptions<Chunk> _,
                    CancellationToken _) =>
                {
                    var chunk = Chunks.FirstOrDefault(((ExpressionFilterDefinition<Chunk>)filter).Expression.Compile());
                    if (chunk is not null && chunk.Pins.All(p => p.Id != pin.Id))
                        chunk.AddPin(pin);
                    return Task.FromResult(chunk);
                });

            // Chunk pins: lookups by id and predicate, creations getting an id.
            ChunkPinsMock.Setup(r => r.FindOneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => ChunkPins.Single(p => p.Id == id));
            ChunkPinsMock.Setup(r => r.TryFindOneAsync(
                    It.IsAny<Expression<Func<ChunkPin, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<ChunkPin, bool>> predicate, CancellationToken _) =>
                    ChunkPins.FirstOrDefault(predicate.Compile()));
            ChunkPinsMock.Setup(r => r.CreateAsync(It.IsAny<ChunkPin>(), It.IsAny<CancellationToken>()))
                .Callback((ChunkPin pin, CancellationToken _) => AddPin(pin))
                .Returns(Task.CompletedTask);

            // The legacy GridFS bucket holds nothing.
            ChunksBucketMock.Setup(b => b.DownloadAsBytesByNameAsync(
                    It.IsAny<string>(),
                    It.IsAny<GridFSDownloadByNameOptions>(),
                    It.IsAny<CancellationToken>()))
                .ThrowsAsync(new GridFSFileNotFoundException("chunk", -1));

            // The engine, for the db execution context handlers the flows open.
            var engineMock = new Mock<IDbContextEngine>();
            engineMock.Setup(e => e.ExecutionContext).Returns(AsyncLocalContext.Instance);

            DbContextMock.Setup(c => c.ChunkPins).Returns(ChunkPinsMock.Object);
            DbContextMock.Setup(c => c.Chunks).Returns(ChunksMock.Object);
            DbContextMock.Setup(c => c.ChunksBucket).Returns(ChunksBucketMock.Object);
            DbContextMock.Setup(c => c.Engine).Returns(engineMock.Object);
            DbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        // Properties.
        public List<ChunkPin> ChunkPins { get; } = [];
        public Mock<IRepository<ChunkPin, string>> ChunkPinsMock { get; } = new();
        public List<Chunk> Chunks { get; } = [];
        public Mock<IGridFSBucket> ChunksBucketMock { get; } = new();
        public Mock<IRepository<Chunk, string>> ChunksMock { get; } = new();
        public IBeehiveDbContext DbContext => DbContextMock.Object;
        public Mock<IBeehiveDbContext> DbContextMock { get; } = new();

        // Methods.
        /// <summary>
        /// Add a chunk to the db, with an id when it has none
        /// </summary>
        public void AddChunk(Chunk chunk)
        {
            ArgumentNullException.ThrowIfNull(chunk);

            if (chunk.Id is null)
                ReflectionHelper.SetValue(chunk, c => c.Id, Guid.NewGuid().ToString());
            Chunks.Add(chunk);
        }

        /// <summary>
        /// Add a pin to the db, with an id when it has none
        /// </summary>
        public void AddPin(ChunkPin pin)
        {
            ArgumentNullException.ThrowIfNull(pin);

            if (pin.Id is null)
                ReflectionHelper.SetValue(pin, p => p.Id, Guid.NewGuid().ToString());
            ChunkPins.Add(pin);
        }
    }
}
