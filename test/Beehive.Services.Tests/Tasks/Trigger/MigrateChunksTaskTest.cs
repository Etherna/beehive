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

using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Helpers;
using Etherna.MongoDB.Bson;
using Etherna.MongoDB.Driver;
using Etherna.Scrinium.Core.Repositories;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Models;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Services.Tasks.Trigger
{
    public class MigrateChunksTaskTest
    {
        // Consts.
        private const string MigrationId = "migration-1";
        private const string OwnerPrivateKey = "0x4f3edf983ac636a65a842ce7c78d9aa706d3b113bce9c46f30d7d21715b23b1d";
        private static readonly DateTime StoredDateTime = new(2024, 3, 1, 0, 0, 0, DateTimeKind.Utc);

        // Fields.
        private readonly InMemoryBeehiveDb db = new();
        private readonly CapturingLogger<MigrateChunksTask> logger = new();
        private ChunksMigration migration = NewMigration();
        private readonly FakeDeprecatedChunkStores stores = new();
        private readonly MigrateChunksTask task;

        // Constructor.
        public MigrateChunksTaskTest()
        {
            // The migration under run, and the reads of its stop request.
            var migrationsMock = new Mock<IRepository<ChunksMigration, string>>();
            migrationsMock.Setup(r => r.FindOneAsync(MigrationId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(() => migration);
            migrationsMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<ChunksMigration>, Task<List<DateTime?>>>>(),
                    null))
                .Returns((Func<IQueryable<ChunksMigration>, Task<List<DateTime?>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(new List<ChunksMigration> { migration })));
            db.DbContextMock.Setup(c => c.ChunksMigrations).Returns(migrationsMock.Object);

            // The lease of the migration is granted.
            db.DbContextMock.Setup(c => c.TryAcquireResourceLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ResourceLockMode>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(() => new FakeResourceLockLease());

            task = new MigrateChunksTask(db.DbContext, stores, logger)
            {
                LeaseRetryPeriod = TimeSpan.FromMilliseconds(1),
                PageSize = 2
            };
        }

        // Tests.

        [Fact]
        public async Task MovesTheDeprecatedChunksWithTheirPinsAndEmptiesTheCollection()
        {
            // Setup.
            var pin = NewPin();
            var soc = AddLegacyChunk(1, isSoc: true, pins: pin);
            AddLegacyChunk(2, pins: pin);
            AddLegacyChunk(3);

            // Action.
            await task.RunAsync(MigrationId, CancellationToken.None);

            // Assert.
            Assert.Empty(stores.LegacyChunks);
            Assert.Equal(3, db.SwarmChunks.Count);
            var movedSoc = db.SwarmChunks.Single(c => c.Id == soc.Hash);
            Assert.True(movedSoc.IsSoc);
            Assert.Equal(soc.Payload, movedSoc.Payload.ToArray());
            Assert.Equal(StoredDateTime, movedSoc.CreationDateTime); //stored first then, not now
            Assert.Equal([pin], movedSoc.Pins);
            Assert.Empty(db.SwarmChunks.Single(c => c.Id == Hash(3)).Pins);

            Assert.Equal(ChunksMigrationState.Completed, migration.State);
            Assert.Equal(3, migration.LegacyChunksTotal);
            Assert.Equal(3, migration.LegacyChunksRead);
            Assert.Equal(3, migration.LegacyChunksInserted);
            Assert.Equal(0, migration.LegacyChunksMerged);
            Assert.Equal(0, migration.LegacyChunksFailed);
        }

        [Fact]
        public async Task AddsThePinsOfADeprecatedChunkToTheChunkHeldAlready()
        {
            // Setup.
            var heldPin = NewPin();
            var movedPin = NewPin();
            var held = new Chunk(Hash(1), Payload(1), false);
            held.AddPin(heldPin);
            db.AddChunk(held);
            AddLegacyChunk(1, pins: movedPin);
            AddLegacyChunk(2);

            // Action.
            await task.RunAsync(MigrationId, CancellationToken.None);

            // Assert.
            Assert.Empty(stores.LegacyChunks);
            Assert.Equal(2, db.SwarmChunks.Count);
            Assert.Equal(new[] { heldPin, movedPin }.ToHashSet(), held.Pins.ToHashSet());
            Assert.Equal(1, migration.LegacyChunksInserted);
            Assert.Equal(1, migration.LegacyChunksMerged);
        }

        [Fact]
        public async Task MovesTwoDocumentsOfTheSameHashIntoOneChunkWithTheirPins()
        {
            // Setup: the duplicated documents of a hash, in the same page.
            var pinA = NewPin();
            var pinB = NewPin();
            AddLegacyChunk(1, pins: pinA);
            AddLegacyChunk(1, pins: pinB);

            // Action.
            await task.RunAsync(MigrationId, CancellationToken.None);

            // Assert.
            Assert.Empty(stores.LegacyChunks);
            var chunk = Assert.Single(db.SwarmChunks);
            Assert.Equal(new[] { pinA, pinB }.ToHashSet(), chunk.Pins.ToHashSet());
            Assert.Equal(1, migration.LegacyChunksInserted);
            Assert.Equal(1, migration.LegacyChunksMerged);
        }

        [Fact]
        public async Task MovesAgainADocumentThatTookAPinBeforeItsDelete()
        {
            // Setup: a pinning adds a pin to a document after its page was saved, before the page is deleted.
            var pin = NewPin();
            var latePin = NewPin();
            var pinned = AddLegacyChunk(1, pins: pin);
            AddLegacyChunk(2);
            stores.OnDeletingLegacyChunks = _ =>
            {
                if (!pinned.Pins.Contains(latePin))
                    pinned.Pins.Add(latePin);
            };

            // Action.
            await task.RunAsync(MigrationId, CancellationToken.None);

            // Assert.
            Assert.Empty(stores.LegacyChunks);
            var chunk = db.SwarmChunks.Single(c => c.Id == pinned.Hash);
            Assert.Equal(new[] { pin, latePin }.ToHashSet(), chunk.Pins.ToHashSet()); //the late pin is not lost
            Assert.Equal(2, migration.LegacyChunksRead); //counted once
            Assert.Equal(2, migration.LegacyChunksInserted);
            Assert.Equal(0, migration.LegacyChunksMerged);
        }

        [Fact]
        public async Task LeavesADocumentThatCantBeReadAndMovesTheOthers()
        {
            // Setup.
            AddLegacyChunk(1);
            stores.LegacyChunks.Add(new FakeDeprecatedChunkStores.StoredLegacyChunk(
                ObjectIdOf(2), Hash(2), Payload(2), false, StoredDateTime)
            {
                ReadFailure = "FormatException: not a chunk"
            });
            AddLegacyChunk(3);

            // Action.
            await task.RunAsync(MigrationId, CancellationToken.None);

            // Assert.
            var left = Assert.Single(stores.LegacyChunks);
            Assert.Equal(ObjectIdOf(2), left.Id);
            Assert.Equal(new[] { Hash(1), Hash(3) }.ToHashSet(), db.SwarmChunks.Select(c => c.Id).ToHashSet());
            Assert.Equal(ChunksMigrationState.Completed, migration.State);
            Assert.Equal(3, migration.LegacyChunksRead);
            Assert.Equal(2, migration.LegacyChunksInserted);
            Assert.Equal(1, migration.LegacyChunksFailed);
        }

        [Fact]
        public async Task LeavesADocumentWhosePayloadIsNotTheChunkOfItsHashAndMovesTheOthers()
        {
            // Setup: a document carrying the payload of another chunk.
            AddLegacyChunk(1);
            stores.LegacyChunks.Add(new FakeDeprecatedChunkStores.StoredLegacyChunk(
                ObjectIdOf(2), Hash(2), Payload(5), false, StoredDateTime));
            AddLegacyChunk(3);

            // Action.
            await task.RunAsync(MigrationId, CancellationToken.None);

            // Assert.
            var left = Assert.Single(stores.LegacyChunks);
            Assert.Equal(ObjectIdOf(2), left.Id);
            Assert.Equal(new[] { Hash(1), Hash(3) }.ToHashSet(), db.SwarmChunks.Select(c => c.Id).ToHashSet());
            Assert.Equal(ChunksMigrationState.Completed, migration.State);
            Assert.Equal(3, migration.LegacyChunksRead);
            Assert.Equal(2, migration.LegacyChunksInserted);
            Assert.Equal(1, migration.LegacyChunksFailed);
            var failure = Assert.Single(logger.Events, e => e.EventName == "LegacyChunkMigrationFailed");
            Assert.Contains($"is not the content addressed chunk of {Hash(2)}", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task LeavesASingleOwnerChunkWhoseContentIsNotTheSignedOne()
        {
            // Setup: a single owner chunk with a byte of its content changed after its owner signed it.
            var soc = NewSoc(1);
            var payload = soc.GetFullPayloadToByteArray();
            payload[^1] ^= 0xff;
            stores.LegacyChunks.Add(new FakeDeprecatedChunkStores.StoredLegacyChunk(
                ObjectIdOf(1), soc.Hash, payload, true, StoredDateTime));

            // Action.
            await task.RunAsync(MigrationId, CancellationToken.None);

            // Assert.
            Assert.Single(stores.LegacyChunks);
            Assert.Empty(db.SwarmChunks);
            Assert.Equal(1, migration.LegacyChunksFailed);
            var failure = Assert.Single(logger.Events, e => e.EventName == "LegacyChunkMigrationFailed");
            Assert.Contains($"is not the single owner chunk of {soc.Hash}", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task LeavesAChunkStoredAsTheOtherKindOfChunk()
        {
            // Setup: a single owner chunk whose document says it is a content addressed one.
            var soc = NewSoc(1);
            stores.LegacyChunks.Add(new FakeDeprecatedChunkStores.StoredLegacyChunk(
                ObjectIdOf(1), soc.Hash, soc.GetFullPayloadToByteArray(), false, StoredDateTime));

            // Action.
            await task.RunAsync(MigrationId, CancellationToken.None);

            // Assert.
            Assert.Single(stores.LegacyChunks);
            Assert.Empty(db.SwarmChunks);
            Assert.Equal(1, migration.LegacyChunksFailed);
            var failure = Assert.Single(logger.Events, e => e.EventName == "LegacyChunkMigrationFailed");
            Assert.Contains(
                $"is the single owner chunk of {soc.Hash}, but it is stored as a content addressed one",
                failure.Message,
                StringComparison.Ordinal);
        }

        [Fact]
        public async Task MovesTheGridFsFilesAsContentChunksAfterTheDeprecatedChunks()
        {
            // Setup: a file held already as a deprecated chunk, a new one, and one whose name is not a hash.
            var pin = NewPin();
            AddLegacyChunk(1, pins: pin);
            AddGridFsFile(Hash(1).ToString(), Payload(1));
            AddGridFsFile(Hash(2).ToString(), Payload(2));
            var badFile = AddGridFsFile("not-a-hash", Payload(3));

            // Action.
            await task.RunAsync(MigrationId, CancellationToken.None);

            // Assert.
            Assert.Equal(badFile, Assert.Single(stores.GridFsFiles));
            Assert.Equal(2, db.SwarmChunks.Count);
            Assert.Equal([pin], db.SwarmChunks.Single(c => c.Id == Hash(1)).Pins); //the file adds no pin
            var fromFile = db.SwarmChunks.Single(c => c.Id == Hash(2));
            Assert.False(fromFile.IsSoc);
            Assert.Equal(Payload(2), fromFile.Payload.ToArray());
            Assert.Equal(StoredDateTime, fromFile.CreationDateTime);

            Assert.Equal(ChunksMigrationState.Completed, migration.State);
            Assert.Equal(3, migration.GridFsFilesTotal);
            Assert.Equal(3, migration.GridFsFilesRead);
            Assert.Equal(1, migration.GridFsFilesInserted);
            Assert.Equal(1, migration.GridFsFilesMerged);
            Assert.Equal(1, migration.GridFsFilesFailed);
        }

        [Fact]
        public async Task LeavesAGridFsFileWhoseContentIsNotTheChunkOfItsName()
        {
            // Setup: a file carrying the payload of another chunk.
            var badFile = AddGridFsFile(Hash(1).ToString(), Payload(5));
            AddGridFsFile(Hash(2).ToString(), Payload(2));

            // Action.
            await task.RunAsync(MigrationId, CancellationToken.None);

            // Assert.
            Assert.Equal(badFile, Assert.Single(stores.GridFsFiles));
            Assert.Equal(Hash(2), Assert.Single(db.SwarmChunks).Id);
            Assert.Equal(1, migration.GridFsFilesInserted);
            Assert.Equal(1, migration.GridFsFilesFailed);
            var failure = Assert.Single(logger.Events, e => e.EventName == "GridFsChunkFileMigrationFailed");
            Assert.Contains($"is not the content addressed chunk of {Hash(1)}", failure.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task StopsAtTheEndOfThePageWhenAStopIsAsked()
        {
            // Setup: the stop is asked while the first page is migrating.
            for (var i = 1; i <= 5; i++)
                AddLegacyChunk(i);
            AddGridFsFile(Hash(9).ToString(), Payload(9));
            stores.OnLegacyChunksPageRead = () =>
            {
                if (!migration.StopRequestedDateTime.HasValue)
                    migration.RequestStop();
            };

            // Action.
            await task.RunAsync(MigrationId, CancellationToken.None);

            // Assert.
            Assert.Equal(ChunksMigrationState.Stopped, migration.State);
            Assert.Equal(2, db.SwarmChunks.Count); //the first page, completed
            Assert.Equal(3, stores.LegacyChunks.Count);
            Assert.Single(stores.GridFsFiles);
            Assert.Equal(2, migration.LegacyChunksInserted);
        }

        [Fact]
        public async Task GoesOnWithWhatThePreviousRunLeft()
        {
            // Setup: a stopped run, and a new migration.
            for (var i = 1; i <= 5; i++)
                AddLegacyChunk(i);
            migration.RequestStop();
            await task.RunAsync(MigrationId, CancellationToken.None);
            Assert.Equal(ChunksMigrationState.Stopped, migration.State);

            migration = NewMigration();

            // Action.
            await task.RunAsync(MigrationId, CancellationToken.None);

            // Assert.
            Assert.Equal(ChunksMigrationState.Completed, migration.State);
            Assert.Equal(5, migration.LegacyChunksTotal);
            Assert.Equal(5, db.SwarmChunks.Count);
            Assert.Empty(stores.LegacyChunks);
        }

        [Fact]
        public async Task FailsTheMigrationWhenAStoreFails()
        {
            // Setup.
            AddLegacyChunk(1);
            stores.OnDeletingLegacyChunks = _ => throw new MongoException("connection lost");

            // Action.
            var exception = await Assert.ThrowsAsync<MongoException>(() => task.RunAsync(MigrationId, CancellationToken.None));

            // Assert.
            Assert.Equal(ChunksMigrationState.Failed, migration.State);
            Assert.Equal(exception.Message, migration.FailureReason);
            Assert.Single(stores.LegacyChunks); //what is not deleted is migrated again by the next run
        }

        [Fact]
        public async Task DoesNothingOnACompletedMigration()
        {
            // Setup: a re-run of the job.
            AddLegacyChunk(1);
            migration.Start(1, 0);
            migration.Complete();

            // Action.
            await task.RunAsync(MigrationId, CancellationToken.None);

            // Assert.
            Assert.Empty(db.SwarmChunks);
            Assert.Single(stores.LegacyChunks);
        }

        // Helpers.
        private FakeDeprecatedChunkStores.StoredGridFsFile AddGridFsFile(string fileName, byte[] content)
        {
            var file = new FakeDeprecatedChunkStores.StoredGridFsFile(ObjectId.GenerateNewId(), fileName, content, StoredDateTime);
            stores.GridFsFiles.Add(file);
            return file;
        }

        /// <summary>
        /// Add the document of the chunk of a seed, as the migration must find it: its payload is the chunk of its hash
        /// </summary>
        private FakeDeprecatedChunkStores.StoredLegacyChunk AddLegacyChunk(int seed, bool isSoc = false, params ChunkPin[] pins)
        {
            var soc = isSoc ? NewSoc(seed) : null;
            var chunk = new FakeDeprecatedChunkStores.StoredLegacyChunk(
                ObjectIdOf(stores.LegacyChunks.Count + 1),
                soc?.Hash ?? Hash(seed),
                soc?.GetFullPayloadToByteArray() ?? Payload(seed),
                isSoc,
                StoredDateTime);
            chunk.Pins.AddRange(pins);
            stores.LegacyChunks.Add(chunk);
            return chunk;
        }

        /// <summary>
        /// The hash of the content addressed chunk of a seed
        /// </summary>
        private static SwarmHash Hash(int seed) => new SwarmChunkBmt().Hash(Payload(seed));

        private static ChunksMigration NewMigration()
        {
            var newMigration = new ChunksMigration();
            ReflectionHelper.SetValue(newMigration, m => m.Id, MigrationId);
            return newMigration;
        }

        /// <summary>
        /// The single owner chunk of a seed: it wraps the content addressed chunk of the seed, signed by its owner
        /// </summary>
        private static SwarmSoc NewSoc(int seed)
        {
            var signer = new PrivateKeySigner(new EthPrivateKey(OwnerPrivateKey));
            SwarmSocIdentifier identifier = Enumerable.Repeat((byte)seed, SwarmSocIdentifier.IdentifierSize).ToArray();
            var soc = new SwarmSoc(identifier, signer.PublicAddress, new SwarmCac(Hash(seed), Payload(seed)));
            soc.Sign(signer, new Hasher());
            return soc;
        }

        private static ChunkPin NewPin()
        {
            var pin = new ChunkPin(null);
            ReflectionHelper.SetValue(pin, p => p.Id, ObjectId.GenerateNewId().ToString());
            return pin;
        }

        private static string ObjectIdOf(int seed) => $"{seed:x24}";

        /// <summary>
        /// The payload of the content addressed chunk of a seed: its span and its data
        /// </summary>
        private static byte[] Payload(int seed) =>
            SwarmCac.BuildFromData(SwarmHash.Zero, new[] { (byte)seed, (byte)seed }).SpanData.ToArray();
    }
}
