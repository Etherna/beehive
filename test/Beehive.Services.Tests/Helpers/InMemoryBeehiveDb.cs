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
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Repositories;
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
    /// A mocked db context whose chunk and pin repositories run over in-memory lists: the lookups run their
    /// predicates and queries over the lists, the creations land in them with an id
    /// </summary>
    internal sealed class InMemoryBeehiveDb
    {
        // Constructor.
        public InMemoryBeehiveDb()
        {
            // Chunks.
            ChunksMock.Setup(r => r.CreateAsync(It.IsAny<Chunk>(), It.IsAny<CancellationToken>()))
                .Callback((Chunk chunk, CancellationToken _) => Add(Chunks, chunk))
                .Returns(Task.CompletedTask);
            ChunksMock.Setup(r => r.CreateAsync(It.IsAny<IEnumerable<Chunk>>(), It.IsAny<CancellationToken>()))
                .Callback((IEnumerable<Chunk> chunks, CancellationToken _) =>
                {
                    foreach (var chunk in chunks)
                        Add(Chunks, chunk);
                })
                .Returns(Task.CompletedTask);
            ChunksMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<Chunk>, Task<List<Chunk>>>>(),
                    null))
                .Returns((Func<IQueryable<Chunk>, Task<List<Chunk>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(Chunks)));
            ChunksMock.Setup(r => r.TryFindOneAndAddToSetAsync(
                    It.IsAny<FilterDefinition<Chunk>>(),
                    It.IsAny<Expression<Func<Chunk, IEnumerable<ChunkPin>>>>(),
                    It.IsAny<ChunkPin>(),
                    It.IsAny<FindOneAndUpdateOptions<Chunk>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((
                    FilterDefinition<Chunk> filter,
                    Expression<Func<Chunk, IEnumerable<ChunkPin>>> _,
                    ChunkPin pin,
                    FindOneAndUpdateOptions<Chunk> _,
                    CancellationToken _) =>
                {
                    var chunk = Chunks.FirstOrDefault(((ExpressionFilterDefinition<Chunk>)filter).Expression.Compile());
                    chunk?.AddPin(pin);
                    return chunk;
                });
            ChunksMock.Setup(r => r.TryFindOneAsync(
                    It.IsAny<Expression<Func<Chunk, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<Chunk, bool>> predicate, CancellationToken _) =>
                    Chunks.FirstOrDefault(predicate.Compile()));

            // Chunk pins.
            ChunkPinsMock.Setup(r => r.CreateAsync(It.IsAny<ChunkPin>(), It.IsAny<CancellationToken>()))
                .Callback((ChunkPin pin, CancellationToken _) => Add(ChunkPins, pin))
                .Returns(Task.CompletedTask);
            ChunkPinsMock.Setup(r => r.FindOneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => ChunkPins.Single(p => p.Id == id));
            ChunkPinsMock.Setup(r => r.TryFindOneAsync(
                    It.IsAny<Expression<Func<ChunkPin, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<ChunkPin, bool>> predicate, CancellationToken _) =>
                    ChunkPins.FirstOrDefault(predicate.Compile()));

            // Db context.
            DbContextMock.Setup(c => c.ChunkPins).Returns(ChunkPinsMock.Object);
            DbContextMock.Setup(c => c.Chunks).Returns(ChunksMock.Object);
            DbContextMock.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);
        }

        // Properties.
        public List<ChunkPin> ChunkPins { get; } = [];
        public Mock<IRepository<ChunkPin, string>> ChunkPinsMock { get; } = new();
        public List<Chunk> Chunks { get; } = [];
        public Mock<IRepository<Chunk, string>> ChunksMock { get; } = new();
        public IBeehiveDbContext DbContext => DbContextMock.Object;
        public Mock<IBeehiveDbContext> DbContextMock { get; } = new();

        // Helpers.
        private static void Add<TModel>(List<TModel> models, TModel model)
            where TModel : EntityModelBase<string>
        {
            ReflectionHelper.SetValue(model, m => m.Id, Guid.NewGuid().ToString());
            models.Add(model);
        }
    }
}
