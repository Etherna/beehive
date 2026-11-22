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

using Etherna.Beehive.Domain.Exceptions;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Persistence;
using Etherna.Beehive.Services.Helpers;
using Etherna.Beehive.Services.Utilities;
using Etherna.MongoDB.Driver;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Repositories;
using Hangfire;
using Hangfire.Common;
using Hangfire.States;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Services.Domain
{
    public class ChunksMigrationServiceTest
    {
        // Consts.
        private const string JobId = "job-1";

        // Fields.
        private readonly Mock<IBackgroundJobClient> backgroundJobClientMock = new();
        private readonly Mock<IHangfireJobsMonitor> hangfireJobsMonitorMock = new();
        private readonly List<ChunksMigration> migrations = [];
        private readonly ChunksMigrationService service;
        private readonly FakeDeprecatedChunkStores stores = new();

        // Constructor.
        public ChunksMigrationServiceTest()
        {
            // The migrations in memory: queries run over them, creations get an id.
            var migrationsMock = new Mock<IRepository<ChunksMigration, string>>();
            migrationsMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<ChunksMigration>, Task<List<ChunksMigration>>>>(),
                    null))
                .Returns((Func<IQueryable<ChunksMigration>, Task<List<ChunksMigration>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(migrations)));
            migrationsMock.Setup(r => r.FindOneAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((string id, CancellationToken _) => migrations.Single(m => m.Id == id));
            migrationsMock.Setup(r => r.CreateAsync(It.IsAny<ChunksMigration>(), It.IsAny<CancellationToken>()))
                .Callback((ChunksMigration migration, CancellationToken _) =>
                {
                    ReflectionHelper.SetValue(migration, m => m.Id, Guid.NewGuid().ToString());
                    ReflectionHelper.SetValue(migration, m => m.CreationDateTime, DateTime.UtcNow.AddTicks(migrations.Count));
                    migrations.Add(migration);
                })
                .Returns(Task.CompletedTask);
            var dbContextMock = new Mock<IBeehiveDbContext>();
            dbContextMock.Setup(c => c.ChunksMigrations).Returns(migrationsMock.Object);

            backgroundJobClientMock.Setup(c => c.Create(It.IsAny<Job>(), It.IsAny<IState>())).Returns(JobId);
            hangfireJobsMonitorMock.Setup(m => m.IsJobActive(JobId)).Returns(true);

            service = new ChunksMigrationService(
                backgroundJobClientMock.Object,
                dbContextMock.Object,
                stores,
                hangfireJobsMonitorMock.Object,
                NullLogger<ChunksMigrationService>.Instance);
        }

        // Tests.

        [Fact]
        public async Task StartsAMigrationAsABackgroundJob()
        {
            // Action.
            var migration = await service.StartMigrationAsync();

            // Assert.
            Assert.Equal(JobId, migration.JobId);
            Assert.Equal(ChunksMigrationState.Pending, migration.State);
            Assert.Equal(migration, Assert.Single(migrations));
            backgroundJobClientMock.Verify(c => c.Create(
                    It.Is<Job>(j => j.Type == typeof(Tasks.Trigger.IMigrateChunksTask) && (string)j.Args[0] == migration.Id),
                    It.IsAny<IState>()),
                Times.Once);
        }

        [Fact]
        public async Task RefusesToStartAMigrationWhileAnotherOneRuns()
        {
            // Setup.
            await service.StartMigrationAsync();

            // Action and assert.
            await Assert.ThrowsAsync<ResourceLockException>(() => service.StartMigrationAsync());
            Assert.Single(migrations);
        }

        [Fact]
        public async Task StartsAMigrationAfterTheLastOneEnded()
        {
            // Setup.
            var stopped = await service.StartMigrationAsync();
            stopped.Start(10, 0);
            stopped.StopOnRequest();

            // Action.
            var migration = await service.StartMigrationAsync();

            // Assert.
            Assert.Equal(2, migrations.Count);
            Assert.Equal(migration, await service.TryGetLatestMigrationAsync());
        }

        [Fact]
        public async Task FailsAMigrationWhoseJobIsGoneAndLetsANewOneStart()
        {
            // Setup: the server running the job stopped.
            var orphan = await service.StartMigrationAsync();
            hangfireJobsMonitorMock.Setup(m => m.IsJobActive(JobId)).Returns(false);

            // Action.
            var latest = await service.TryGetLatestMigrationAsync();

            // Assert.
            Assert.Equal(orphan, latest);
            Assert.Equal(ChunksMigrationState.Failed, orphan.State);
            hangfireJobsMonitorMock.Setup(m => m.IsJobActive(JobId)).Returns(true);
            await service.StartMigrationAsync(); //not refused
            Assert.Equal(2, migrations.Count);
        }

        [Fact]
        public async Task AsksARunningMigrationToStop()
        {
            // Setup.
            var migration = await service.StartMigrationAsync();
            migration.Start(10, 0);

            // Action.
            var result = await service.StopMigrationAsync(migration.Id);

            // Assert.
            Assert.NotNull(result.StopRequestedDateTime);
            Assert.Equal(ChunksMigrationState.Running, result.State); //its job stops it
        }

        [Fact]
        public async Task AnswersAnEndedMigrationAsItIsOnAStop()
        {
            // Setup.
            var migration = await service.StartMigrationAsync();
            migration.Start(10, 0);
            migration.Complete();

            // Action.
            var result = await service.StopMigrationAsync(migration.Id);

            // Assert.
            Assert.Null(result.StopRequestedDateTime);
            Assert.Equal(ChunksMigrationState.Completed, result.State);
        }

        [Fact]
        public async Task CountsTheChunksStillInTheDeprecatedStores()
        {
            // Setup.
            stores.LegacyChunks.Add(new FakeDeprecatedChunkStores.StoredLegacyChunk(
                "000000000000000000000001", "aa00000000000000000000000000000000000000000000000000000000000001", [1], false, DateTime.UtcNow));
            stores.GridFsFiles.Add(new FakeDeprecatedChunkStores.StoredGridFsFile(
                MongoDB.Bson.ObjectId.GenerateNewId(), "f", [1], DateTime.UtcNow));
            stores.GridFsFiles.Add(new FakeDeprecatedChunkStores.StoredGridFsFile(
                MongoDB.Bson.ObjectId.GenerateNewId(), "g", [1], DateTime.UtcNow));

            // Action.
            var count = await service.CountDeprecatedChunksAsync();

            // Assert.
            Assert.Equal(1, count.LegacyChunks);
            Assert.Equal(2, count.GridFsFiles);
        }
    }
}
