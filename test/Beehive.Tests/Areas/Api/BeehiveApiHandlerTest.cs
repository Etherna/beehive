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

using Etherna.Beehive.Areas.Api.InputModels;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Helpers;
using Etherna.Beehive.Persistence;
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Utilities;
using Etherna.MongoDB.Driver;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Repositories;
using Etherna.SwarmSdk.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Areas.Api
{
    public class BeehiveApiHandlerTest
    {
        // Consts.
        private const string CheckId = "check-1";

        // Fields.
        private readonly BeehiveApiHandler handler;
        private readonly List<PinCheck> pinChecks = [];
        private readonly Mock<IPinsCheckService> pinsCheckServiceMock = new();

        // Constructor.
        public BeehiveApiHandlerTest()
        {
            // The pin records of the checks in memory: the query of the handler runs as it is over them.
            var pinChecksMock = new Mock<IRepository<PinCheck, string>>();
            pinChecksMock.Setup(r => r.QueryElementsAsync(
                    It.IsAny<Func<IQueryable<PinCheck>, Task<List<PinCheck>>>>(),
                    null))
                .Returns((Func<IQueryable<PinCheck>, Task<List<PinCheck>>> query, AggregateOptions? _) =>
                    query(InMemoryQueryable.Of(pinChecks)));
            var dbContextMock = new Mock<IBeehiveDbContext>();
            dbContextMock.Setup(c => c.PinChecks).Returns(pinChecksMock.Object);

            handler = new BeehiveApiHandler(
                dbContextMock.Object,
                new Mock<IBeeNodeLiveManager>().Object,
                new Mock<IChunksMigrationService>().Object,
                NullLogger<BeehiveApiHandler>.Instance,
                new Mock<INodePinsImportService>().Object,
                pinsCheckServiceMock.Object);
        }

        // Tests.

        [Theory]
        [InlineData(null)]
        [InlineData(true)]
        [InlineData(false)]
        public async Task DeletePinsChecksAnswersTheNumberOfDeletedChecks(bool? failed)
        {
            // Setup.
            pinsCheckServiceMock.Setup(s => s.DeleteChecksAsync(failed, It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            // Action.
            var result = await handler.DeletePinsChecksAsync(failed, CancellationToken.None);
            var (statusCode, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            Assert.Equal("3", body);
        }

        [Fact]
        public async Task GetPinsCheckAnswersNotFoundForACheckBeehiveDoesNotHave()
        {
            // Setup.
            pinsCheckServiceMock.Setup(s => s.GetCheckAsync("unknown", It.IsAny<CancellationToken>()))
                .ThrowsAsync(new KeyNotFoundException());

            // Action.
            var result = await handler.GetPinsCheckAsync("unknown", CancellationToken.None);
            var (statusCode, _) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status404NotFound, statusCode);
        }

        [Fact]
        public async Task GetPinsCheckAnswersTheCheckOfTheId()
        {
            // Setup.
            var check = NewCheck(CheckId);
            check.Start(7, [PinCheckOutcome.Complete, PinCheckOutcome.Incomplete]);
            pinsCheckServiceMock.Setup(s => s.GetCheckAsync(CheckId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(check);

            // Action.
            var result = await handler.GetPinsCheckAsync(CheckId, CancellationToken.None);
            var (statusCode, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            var json = JsonDocument.Parse(body).RootElement;
            Assert.Equal(CheckId, json.GetProperty("id").GetString());
            Assert.Equal("Running", json.GetProperty("state").GetString());
            Assert.Equal(1, json.GetProperty("completePins").GetInt64());
            Assert.Equal(1, json.GetProperty("incompletePins").GetInt64());
            Assert.Equal(2, json.GetProperty("checkedPins").GetInt64());
            Assert.Equal(7, json.GetProperty("totalPins").GetInt64());
        }

        [Fact]
        public async Task GetPinsChecksAnswersTheAskedPageOfChecksAsTheServiceOrdersThem()
        {
            // Setup.
            pinsCheckServiceMock.Setup(s => s.GetChecksAsync(2, 5, It.IsAny<CancellationToken>()))
                .ReturnsAsync([NewCheck("check-2"), NewCheck("check-1")]);

            // Action.
            var result = await handler.GetPinsChecksAsync(2, 5, CancellationToken.None);
            var (statusCode, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            Assert.Equal(
                ["check-2", "check-1"],
                JsonDocument.Parse(body).RootElement.EnumerateArray().Select(c => c.GetProperty("id").GetString()));
        }

        [Fact]
        public async Task GetPinsCheckPinsAnswersARecordInReadingOrder()
        {
            // Setup.
            var record = AddPinCheck(NewCheck(CheckId), 1, r => r.SetChecked(153, 2, 1));

            // Action.
            var result = await handler.GetPinsCheckPinsAsync(CheckId, null, 0, 100, CancellationToken.None);
            var (statusCode, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            Assert.Equal(
                $$$"""[{"id":"{{{record.Id}}}","checkId":"check-1","reference":"{{{Reference(1)}}}","outcome":"Invalid","missing":2,"invalid":1,"total":153,"failureReason":null,"creationDateTime":"2026-11-23T10:01:00Z"}]""",
                body);
        }

        [Theory]
        [InlineData(null, new[] { 4, 3, 2, 1 })]
        [InlineData(PinCheckOutcome.Complete, new[] { 1 })]
        [InlineData(PinCheckOutcome.Failed, new[] { 4 })]
        [InlineData(PinCheckOutcome.Incomplete, new[] { 2 })]
        [InlineData(PinCheckOutcome.Invalid, new[] { 3 })]
        public async Task GetPinsCheckPinsAnswersTheRecordsOfTheAskedOutcomeOnly(PinCheckOutcome? outcome, int[] expectedReferences)
        {
            ArgumentNullException.ThrowIfNull(expectedReferences);

            // Setup.
            //a record of each outcome, and one of another check
            var check = NewCheck(CheckId);
            AddPinCheck(check, 1, r => r.SetChecked(153, 0, 0));
            AddPinCheck(check, 2, r => r.SetChecked(153, 2, 0));
            AddPinCheck(check, 3, r => r.SetChecked(153, 2, 1));
            AddPinCheck(check, 4, r => r.SetFailed("The reference is not pinned"));
            AddPinCheck(NewCheck("check-2"), 5, r => r.SetChecked(153, 0, 0));

            // Action.
            var result = await handler.GetPinsCheckPinsAsync(CheckId, outcome, 0, 100, CancellationToken.None);
            var (_, body) = await ExecuteResultAsync(result);

            // Assert.
            using var json = JsonDocument.Parse(body);
            var records = json.RootElement.EnumerateArray().ToArray();
            Assert.Equal(
                expectedReferences.Select(seed => Reference(seed).ToString()),
                records.Select(r => r.GetProperty("reference").GetString())); //newest first
            if (outcome.HasValue)
                Assert.All(records, r => Assert.Equal(outcome.Value.ToString(), r.GetProperty("outcome").GetString()));
        }

        [Fact]
        public async Task StartPinsCheckAnswersAcceptedWithTheCheckInReadingOrder()
        {
            // Setup.
            ChunkPinState[] pinStates = [ChunkPinState.Incomplete, ChunkPinState.Unprocessed];
            var check = NewCheck(CheckId, pinStates);
            check.SetJobId("job-1");
            pinsCheckServiceMock.Setup(s => s.StartCheckAsync(
                    pinStates,
                    It.Is<IEnumerable<SwarmReference>>(r => !r.Any()),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(check);

            // Action.
            var result = await handler.StartPinsCheckAsync(pinStates, null, CancellationToken.None);
            var (statusCode, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status202Accepted, statusCode);
            Assert.Equal(
                """{"id":"check-1","state":"Pending","requestedPinStates":["Incomplete","Unprocessed"],"requestedReferences":[],"runningReference":null,"completePins":0,"incompletePins":0,"invalidPins":0,"failedPins":0,"checkedPins":0,"totalPins":null,"error":null,"jobId":"job-1","creationDateTime":"2026-11-23T10:00:00Z","startedDateTime":null,"stopRequestedDateTime":null,"completedDateTime":null}""",
                body);
        }

        [Fact]
        public async Task StartPinsCheckPassesTheRequestedReferences()
        {
            // Setup.
            pinsCheckServiceMock.Setup(s => s.StartCheckAsync(
                    It.IsAny<IEnumerable<ChunkPinState>>(),
                    It.IsAny<IEnumerable<SwarmReference>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(NewCheck(CheckId));

            // Action.
            var result = await handler.StartPinsCheckAsync(
                null,
                new PinsCheckInput { References = [Reference(1).ToString(), Reference(2).ToString()] },
                CancellationToken.None);
            var (statusCode, _) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status202Accepted, statusCode);
            pinsCheckServiceMock.Verify(s => s.StartCheckAsync(
                    null,
                    It.Is<IEnumerable<SwarmReference>>(r => r.SequenceEqual(new[] { Reference(1), Reference(2) })),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task StartPinsCheckRefusesAReferenceThatIsNotOne()
        {
            // Action.
            var result = await handler.StartPinsCheckAsync(
                null,
                new PinsCheckInput { References = ["not a reference"] },
                CancellationToken.None);
            var (statusCode, _) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status400BadRequest, statusCode);
            pinsCheckServiceMock.Verify(s => s.StartCheckAsync(
                    It.IsAny<IEnumerable<ChunkPinState>>(),
                    It.IsAny<IEnumerable<SwarmReference>>(),
                    It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task StopPinsCheckAnswersTheCheckAskedToStop()
        {
            // Setup.
            var check = NewCheck(CheckId);
            check.Start(7, []);
            check.RequestStop();
            pinsCheckServiceMock.Setup(s => s.StopCheckAsync(CheckId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(check);

            // Action.
            var result = await handler.StopPinsCheckAsync(CheckId, CancellationToken.None);
            var (statusCode, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status200OK, statusCode);
            var json = JsonDocument.Parse(body).RootElement;
            Assert.Equal(CheckId, json.GetProperty("id").GetString());
            Assert.Equal(JsonValueKind.String, json.GetProperty("stopRequestedDateTime").ValueKind);
        }

        // Helpers.
        /// <summary>
        /// Add the record of a pin to the db, each one a minute after the previous
        /// </summary>
        private PinCheck AddPinCheck(PinsCheck check, int seed, Action<PinCheck> setOutcome)
        {
            var record = new PinCheck(check, Reference(seed));
            ReflectionHelper.SetValue(record, r => r.Id, $"record-{seed}");
            ReflectionHelper.SetValue(record, r => r.CreationDateTime, new DateTime(2026, 11, 23, 10, seed, 0, DateTimeKind.Utc));
            setOutcome(record);
            pinChecks.Add(record);
            return record;
        }

        private static async Task<(int StatusCode, string Body)> ExecuteResultAsync(IResult result)
        {
            var httpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
            };
            await using var bodyStream = new MemoryStream();
            httpContext.Response.Body = bodyStream;

            await result.ExecuteAsync(httpContext);

            return (httpContext.Response.StatusCode, Encoding.UTF8.GetString(bodyStream.ToArray()));
        }

        private static PinsCheck NewCheck(string id, IEnumerable<ChunkPinState>? requestedPinStates = null)
        {
            var check = new PinsCheck(requestedPinStates);
            ReflectionHelper.SetValue(check, c => c.Id, id);
            ReflectionHelper.SetValue(check, c => c.CreationDateTime, new DateTime(2026, 11, 23, 10, 0, 0, DateTimeKind.Utc));
            return check;
        }

        private static SwarmReference Reference(int seed) => $"cc{seed:x62}";
    }
}
