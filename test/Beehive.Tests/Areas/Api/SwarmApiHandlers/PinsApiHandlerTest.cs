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
using Etherna.Beehive.Helpers;
using Etherna.Beehive.Persistence;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Tasks.Trigger;
using Etherna.Beehive.Services.Utilities.Models;
using Etherna.MongoDB.Driver;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Repositories;
using Etherna.Scrinium.Core.Utility;
using Etherna.SwarmSdk.Models;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Areas.Api.SwarmApiHandlers
{
    public class PinsApiHandlerTest
    {
        // Consts.
        private static readonly SwarmHash MissingChunkHash = "bb00000000000000000000000000000000000000000000000000000000000009";
        private static readonly SwarmReference OtherReference = "aa00000000000000000000000000000000000000000000000000000000000002";
        private static readonly SwarmReference Reference = "aa00000000000000000000000000000000000000000000000000000000000001";

        // Fields.
        private readonly List<ChunkPin> chunkPins = [];
        private readonly Mock<IBeehiveDbContext> dbContextMock = new();
        private readonly PinsApiHandler handler;
        private readonly Mock<IPinChunksTask> pinChunksTaskMock = new();
        private readonly Mock<IPinIntegrityService> pinIntegrityServiceMock = new();
        private readonly Mock<IPinService> pinServiceMock = new();

        // Constructor.
        public PinsApiHandlerTest()
        {
            // The pins in memory: lookups run their predicates over them, creations get an id.
            var chunkPinsMock = new Mock<IRepository<ChunkPin, string>>();
            chunkPinsMock.Setup(r => r.TryFindOneAsync(
                    It.IsAny<Expression<Func<ChunkPin, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<ChunkPin, bool>> predicate, CancellationToken _) =>
                    chunkPins.FirstOrDefault(predicate.Compile()));
            chunkPinsMock.Setup(r => r.FindOneAsync(
                    It.IsAny<Expression<Func<ChunkPin, bool>>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((Expression<Func<ChunkPin, bool>> predicate, CancellationToken _) =>
                    chunkPins.First(predicate.Compile()));
            chunkPinsMock.Setup(r => r.CreateAsync(It.IsAny<ChunkPin>(), It.IsAny<CancellationToken>()))
                .Callback((ChunkPin pin, CancellationToken _) =>
                {
                    ReflectionHelper.SetValue(pin, p => p.Id, Guid.NewGuid().ToString());
                    chunkPins.Add(pin);
                })
                .Returns(Task.CompletedTask);
            chunkPinsMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<ChunkPin>, Task<List<ChunkPin>>>>(),
                    null))
                .Returns((Func<IQueryable<ChunkPin>, Task<List<ChunkPin>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(chunkPins)));
            chunkPinsMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<ChunkPin>, Task<List<SwarmReference>>>>(),
                    null))
                .Returns((Func<IQueryable<ChunkPin>, Task<List<SwarmReference>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(chunkPins)));
            dbContextMock.Setup(c => c.ChunkPins).Returns(chunkPinsMock.Object);

            // Every lock is granted, unless a test holds one.
            dbContextMock.Setup(c => c.TryAcquireResourceLockAsync(
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<ResourceLockMode>(),
                    It.IsAny<TimeSpan?>()))
                .ReturnsAsync(() => new Mock<IResourceLockLease>().Object);

            // The pinning itself is the task's: here every pin succeeds.
            pinChunksTaskMock.Setup(t => t.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((string pinId, CancellationToken _) => chunkPins.Single(p => p.Id == pinId).UpdateProcessed([], 3))
                .Returns(Task.CompletedTask);

            // The check of a pin: the reference of the test whole, the other one with a chunk missing. Its
            // correction leaves a pin found incomplete not succeeded, and declares succeeded one found whole. The
            // lock of the pins check is free, unless a test has another check hold it.
            pinIntegrityServiceMock.Setup(s => s.CheckPinsAsync(It.IsAny<SwarmReference>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((SwarmReference reference, bool _, CancellationToken _) => reference == Reference
                    ? new PinCheckResult(Reference, 3, [], [], 0)
                    : new PinCheckResult(reference, 5, [MissingChunkHash], [], 0));
            pinIntegrityServiceMock.Setup(s => s.CorrectPinAsync(It.IsAny<ChunkPin>(), It.IsAny<PinCheckResult>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((ChunkPin pin, PinCheckResult checkResult, CancellationToken _) =>
                {
                    if (!checkResult.IsComplete)
                        pin.UpdateProcessed(checkResult.MissingChunks, checkResult.TotalChunks - checkResult.MissingChunks.Count);
                    else if (pin.State != ChunkPinState.Succeeded)
                        pin.UpdateProcessed([], checkResult.TotalChunks);
                    return pin;
                });
            pinIntegrityServiceMock.Setup(s => s.TryAcquirePinsCheckLockAsync())
                .ReturnsAsync(() => new Mock<IResourceLockLease>().Object);

            handler = new PinsApiHandler(
                new Mock<IBackgroundJobClient>().Object,
                dbContextMock.Object,
                NullLogger<PinsApiHandler>.Instance,
                pinChunksTaskMock.Object,
                pinIntegrityServiceMock.Object,
                pinServiceMock.Object);
        }

        // Tests.

        [Fact]
        public async Task CheckPinsAnswersALineAtZeroForAReferenceBeehiveDoesNotPin()
        {
            // Setup.
            var httpContext = NewHttpContext();

            // Action.
            var result = await handler.CheckPinsAsync(Reference, false, httpContext);
            var (statusCode, contentType, body) = await ExecuteResultAsync(result, httpContext);

            // Assert.
            //as bee, whose check of a reference it doesn't pin finds nothing to count
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            Assert.Equal("application/x-ndjson", contentType);
            Assert.Equal($$$"""{"reference":"{{{Reference}}}","missing":0,"invalid":0,"total":0}""" + "\n", body);
            pinIntegrityServiceMock.Verify(s => s.CheckPinsAsync(It.IsAny<SwarmReference>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CheckPinsAnswersTheCheckOfAReferenceAsALine()
        {
            // Setup.
            AddPin(Reference);
            var httpContext = NewHttpContext();

            // Action.
            var result = await handler.CheckPinsAsync(Reference, false, httpContext);
            var (statusCode, contentType, body) = await ExecuteResultAsync(result, httpContext);

            // Assert.
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            Assert.Equal("application/x-ndjson", contentType);
            Assert.Equal($$$"""{"reference":"{{{Reference}}}","missing":0,"invalid":0,"total":3}""" + "\n", body);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task CheckPinsRefusesACheckWhileADeprecatedStoreHoldsChunks(bool ofAReference)
        {
            // Setup.
            pinIntegrityServiceMock.Setup(s => s.VerifyPinsCanBeCheckedAsync(It.IsAny<CancellationToken>()))
                .ThrowsAsync(new DeprecatedChunksNotMigratedException(
                    "the deprecated chunk stores are not empty: migrate their chunks first"));
            AddPin(Reference);
            var httpContext = NewHttpContext();

            // Action.
            var result = await handler.CheckPinsAsync(ofAReference ? Reference : (SwarmReference?)null, false, httpContext);
            var (statusCode, _, body) = await ExecuteResultAsync(result, httpContext);

            // Assert.
            Assert.Equal(StatusCodes.Status409Conflict, statusCode);
            Assert.Equal(
                """{"code":409,"message":"the deprecated chunk stores are not empty: migrate their chunks first"}""",
                body);
            pinIntegrityServiceMock.Verify(s => s.CheckPinsAsync(It.IsAny<SwarmReference>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CheckPinsRefusesACheckOfEveryPinWhileOneRuns()
        {
            // Setup.
            //the lock of the pins check is held: by another request, or by a background run of the check
            pinIntegrityServiceMock.Setup(s => s.TryAcquirePinsCheckLockAsync())
                .ReturnsAsync((IResourceLockLease?)null);
            AddPin(Reference);
            var httpContext = NewHttpContext();

            // Action.
            var result = await handler.CheckPinsAsync(null, false, httpContext);
            var (statusCode, _, body) = await ExecuteResultAsync(result, httpContext);

            // Assert.
            Assert.Equal(StatusCodes.Status429TooManyRequests, statusCode);
            Assert.Equal("""{"code":429,"message":"pin integrity check of all pins already running"}""", body);
            pinIntegrityServiceMock.Verify(s => s.CheckPinsAsync(It.IsAny<SwarmReference>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task CheckPinsAnswersOneLineForAReferenceWithMoreThanOnePin()
        {
            // Setup.
            //a content uploaded twice with its pin has two pins
            AddPin(Reference);
            AddPin(Reference);
            var httpContext = NewHttpContext();

            // Action.
            var result = await handler.CheckPinsAsync(Reference, false, httpContext);
            var (statusCode, _, body) = await ExecuteResultAsync(result, httpContext);

            // Assert.
            //the content is checked once, and both the pins of its reference are brought to what the check finds
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            Assert.Equal($$$"""{"reference":"{{{Reference}}}","missing":0,"invalid":0,"total":3}""" + "\n", body);
            pinIntegrityServiceMock.Verify(
                s => s.CheckPinsAsync(Reference, It.IsAny<bool>(), It.IsAny<CancellationToken>()),
                Times.Once);
            Assert.All(chunkPins, p => Assert.Equal(ChunkPinState.Succeeded, p.State));
        }

        [Fact]
        public async Task CheckPinsAnswersTheLineOfAPinLockedByAnotherOperation()
        {
            // Setup.
            //the pin is in the hands of another operation, which writes what it holds
            AddPin(OtherReference);
            pinIntegrityServiceMock.Setup(s => s.CorrectPinAsync(It.IsAny<ChunkPin>(), It.IsAny<PinCheckResult>(), It.IsAny<CancellationToken>()))
                .ThrowsAsync(new ResourceLockException("Pin is locked by another operation"));
            var httpContext = NewHttpContext();

            // Action.
            var result = await handler.CheckPinsAsync(OtherReference, false, httpContext);
            var (statusCode, _, body) = await ExecuteResultAsync(result, httpContext);

            // Assert.
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            Assert.Equal($$$"""{"reference":"{{{OtherReference}}}","missing":1,"invalid":0,"total":5}""" + "\n", body);
            Assert.Equal(ChunkPinState.Unprocessed, chunkPins.Single().State); //left to that operation
        }

        [Fact]
        public async Task CheckPinsBringsEveryPinToWhatItsCheckFinds()
        {
            // Setup.
            //a content beehive holds in full, with a pin not processed yet and a second one, and a pin declared
            //succeeded with a chunk missing
            AddPin(Reference);
            AddPin(OtherReference);
            AddPin(Reference);
            chunkPins[1].UpdateProcessed([], 5);
            var httpContext = NewHttpContext();

            // Action.
            var result = await handler.CheckPinsAsync(null, false, httpContext);
            var (statusCode, _, body) = await ExecuteResultAsync(result, httpContext);

            // Assert.
            //the same lines, and the pins corrected as a background run of the check does
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            Assert.Equal(
                $$$"""
                {"reference":"{{{Reference}}}","missing":0,"invalid":0,"total":3}
                {"reference":"{{{OtherReference}}}","missing":1,"invalid":0,"total":5}

                """,
                body);
            Assert.Equal(ChunkPinState.Succeeded, chunkPins[0].State);
            Assert.Equal(3, chunkPins[0].TotPinnedChunks);
            Assert.Equal(ChunkPinState.Incomplete, chunkPins[1].State);
            Assert.Equal([MissingChunkHash], chunkPins[1].MissingChunks);
            Assert.Equal(ChunkPinState.Succeeded, chunkPins[2].State); //the second pin of the first reference
            pinIntegrityServiceMock.Verify(
                s => s.CheckPinsAsync(It.IsAny<SwarmReference>(), true, It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task CheckPinsOfADryCheckWriteNothing()
        {
            // Setup.
            //a pin not processed yet that beehive holds in full, and one declared succeeded with a chunk missing
            AddPin(Reference);
            AddPin(OtherReference);
            chunkPins[1].UpdateProcessed([], 5);
            var httpContext = NewHttpContext();

            // Action.
            var result = await handler.CheckPinsAsync(null, true, httpContext);
            var (statusCode, _, body) = await ExecuteResultAsync(result, httpContext);

            // Assert.
            //the same lines, from a check that attaches no pin to the chunks, and the pins left as they declare
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            Assert.Equal(
                $$$"""
                {"reference":"{{{Reference}}}","missing":0,"invalid":0,"total":3}
                {"reference":"{{{OtherReference}}}","missing":1,"invalid":0,"total":5}

                """,
                body);
            pinIntegrityServiceMock.Verify(
                s => s.CheckPinsAsync(It.IsAny<SwarmReference>(), true, It.IsAny<CancellationToken>()),
                Times.Exactly(2));
            pinIntegrityServiceMock.Verify(
                s => s.CheckPinsAsync(It.IsAny<SwarmReference>(), false, It.IsAny<CancellationToken>()),
                Times.Never);
            pinIntegrityServiceMock.Verify(
                s => s.CorrectPinAsync(It.IsAny<ChunkPin>(), It.IsAny<PinCheckResult>(), It.IsAny<CancellationToken>()),
                Times.Never);
            Assert.Equal(ChunkPinState.Unprocessed, chunkPins[0].State);
            Assert.Equal(ChunkPinState.Succeeded, chunkPins[1].State);
        }

        [Fact]
        public async Task CheckPinsStreamsALinePerPinnedReferenceWithoutAReference()
        {
            // Setup.
            AddPin(Reference);
            AddPin(OtherReference);
            AddPin(Reference); //a second pin of the first reference: one line all the same
            chunkPins.Add(new ChunkPin(null)); //a provisional pin, with no reference to check
            var httpContext = NewHttpContext();

            // Action.
            var result = await handler.CheckPinsAsync(null, false, httpContext);
            var (statusCode, contentType, body) = await ExecuteResultAsync(result, httpContext);

            // Assert.
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            Assert.Equal("application/x-ndjson", contentType);
            Assert.Equal(
                $$$"""
                {"reference":"{{{Reference}}}","missing":0,"invalid":0,"total":3}
                {"reference":"{{{OtherReference}}}","missing":1,"invalid":0,"total":5}

                """,
                body);
        }

        [Fact]
        public async Task CreatePinBeeAnswersCreatedWithBeeStatusBody()
        {
            // Action.
            var result = await handler.CreatePinBeeAsync(Reference);
            var (statusCode, contentType, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status201Created, statusCode);
            Assert.Equal("application/json; charset=utf-8", contentType);
            Assert.Equal("""{"code":201,"message":"Created"}""", body);
        }

        [Fact]
        public async Task CreatePinBeeAnswersOkWithBeeStatusBodyOnAReferenceAlreadyPinned()
        {
            // Setup.
            var pin = new ChunkPin(Reference);
            pin.UpdateProcessed([], 3);
            chunkPins.Add(pin);

            // Action.
            var result = await handler.CreatePinBeeAsync(Reference);
            var (statusCode, contentType, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            Assert.Equal("application/json; charset=utf-8", contentType);
            Assert.Equal("""{"code":200,"message":"OK"}""", body);
            pinChunksTaskMock.Verify(t => t.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never); //nothing to pin again
        }

        [Fact]
        public async Task CreatePinBeeRemovesAPinItsPinningLeavesNotSucceededAndAnswersNotFound()
        {
            // Setup.
            //the pinning ends with a chunk missing: bee has no pin of a content it doesn't hold in full
            pinChunksTaskMock.Setup(t => t.RunAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .Callback((string pinId, CancellationToken _) =>
                    chunkPins.Single(p => p.Id == pinId).UpdateProcessed([MissingChunkHash], 2))
                .Returns(Task.CompletedTask);

            // Action.
            var result = await handler.CreatePinBeeAsync(Reference);
            var (statusCode, _, _) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status404NotFound, statusCode);
            pinServiceMock.Verify(s => s.TryDeletePinAsync(Reference), Times.Once);
        }

        [Fact]
        public async Task GetPinsBeehiveAnswersEveryPinWithItsStateNewestFirst()
        {
            // Setup.
            //a pin not processed yet, and a newer one processed with every chunk
            AddPin(Reference);
            AddPin(OtherReference);
            ReflectionHelper.SetValue(chunkPins[0], p => p.CreationDateTime, new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc));
            ReflectionHelper.SetValue(chunkPins[1], p => p.CreationDateTime, new DateTime(2026, 11, 23, 10, 1, 0, DateTimeKind.Utc));
            chunkPins[1].UpdateProcessed([], 3);

            // Action.
            var result = await handler.GetPinsBeehiveAsync(0, 10);
            var (statusCode, _, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            using var pins = JsonDocument.Parse(body);
            Assert.Equal(
                [(OtherReference.ToString(), "Succeeded", true), (Reference.ToString(), "Unprocessed", false)],
                pins.RootElement.EnumerateArray().Select(p => (
                    p.GetProperty("reference").GetString()!,
                    p.GetProperty("state").GetString()!,
                    p.GetProperty("succeeded").GetBoolean())));
        }

        [Theory]
        [InlineData(ChunkPinState.Unprocessed, false, false, 0)]
        [InlineData(ChunkPinState.Incomplete, true, false, 2)]
        [InlineData(ChunkPinState.Succeeded, true, true, 3)]
        public async Task GetPinStatusBeehiveAnswersThePinWithItsState(
            ChunkPinState state,
            bool expectedProcessed,
            bool expectedSucceeded,
            long expectedPinnedChunks)
        {
            // Setup.
            AddPin(Reference);
            var pin = chunkPins.Single();
            var creationDateTime = new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc);
            ReflectionHelper.SetValue(pin, p => p.CreationDateTime, creationDateTime);
            if (state == ChunkPinState.Incomplete)
                pin.UpdateProcessed([MissingChunkHash], 2);
            else if (state == ChunkPinState.Succeeded)
                pin.UpdateProcessed([], 3);

            // Action.
            var result = await handler.GetPinStatusBeehiveAsync(Reference);
            var (statusCode, _, body) = await ExecuteResultAsync(result);

            // Assert.
            //the state right after what is pinned, and succeeded still there for the clients that read it
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            Assert.Equal(
                "{" +
                $"\"reference\":\"{Reference}\"," +
                $"\"creationTime\":{new DateTimeOffset(creationDateTime).ToUnixTimeSeconds()}," +
                $"\"state\":\"{state}\"," +
                $"\"processed\":{(expectedProcessed ? "true" : "false")}," +
                $"\"succeeded\":{(expectedSucceeded ? "true" : "false")}," +
                $"\"missingChunks\":[{(state == ChunkPinState.Incomplete ? $"\"{MissingChunkHash}\"" : "")}]," +
                $"\"pinnedChunks\":{expectedPinnedChunks}" +
                "}",
                body);
        }

        // Helpers.
        private void AddPin(SwarmReference reference)
        {
            var pin = new ChunkPin(reference);
            ReflectionHelper.SetValue(pin, p => p.Id, Guid.NewGuid().ToString());
            chunkPins.Add(pin);
        }

        private static async Task<(int StatusCode, string? ContentType, string Body)> ExecuteResultAsync(
            IResult result,
            DefaultHttpContext? httpContext = null)
        {
            httpContext ??= NewHttpContext();
            await using var bodyStream = new MemoryStream();
            httpContext.Response.Body = bodyStream;

            await result.ExecuteAsync(httpContext);

            return (
                httpContext.Response.StatusCode,
                httpContext.Response.ContentType,
                Encoding.UTF8.GetString(bodyStream.ToArray()));
        }

        private static DefaultHttpContext NewHttpContext() =>
            new()
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
            };
    }
}
