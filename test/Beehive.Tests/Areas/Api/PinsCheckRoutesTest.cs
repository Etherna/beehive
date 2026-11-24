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
using Etherna.Beehive.Areas.Api.SwarmApiHandlers;
using Etherna.Beehive.Domain.Models;
using Etherna.Beehive.Helpers;
using Etherna.SwarmSdk.Models;
using Microsoft.AspNetCore.Http;
using Moq;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Areas.Api
{
    /// <summary>
    /// The routes of the pins check as the host maps them: both apis on a real server, behind the enum binding of
    /// the host, with handlers that only record how they are called
    /// </summary>
    public sealed class PinsCheckRoutesTest : IClassFixture<ApiRoutesHostFixture>
    {
        // Consts.
        private const string CheckId = "check-1";
        private static readonly SwarmReference Reference = "aa00000000000000000000000000000000000000000000000000000000000001";

        // Fields.
        private readonly Mock<IBeehiveApiHandler> beehiveHandlerMock;
        private readonly HttpClient client;
        private readonly Mock<IPinsApiHandler> pinsHandlerMock;

        // Constructor.
        public PinsCheckRoutesTest(ApiRoutesHostFixture host)
        {
            ArgumentNullException.ThrowIfNull(host);

            // The server is started once for the class: each test starts from handlers never called.
            beehiveHandlerMock = host.BeehiveHandlerMock;
            client = host.Client;
            pinsHandlerMock = host.PinsHandlerMock;
            beehiveHandlerMock.Invocations.Clear();
            pinsHandlerMock.Invocations.Clear();
        }

        // Tests.

        [Theory]
        [InlineData("/pins/check")]
        [InlineData("/v1/pins/check")] //with bee's prefix
        public async Task CheckOfEveryPinCorrectsUnlessAskedDry(string path)
        {
            // Action.
            using var response = await client.GetAsync(new Uri(path, UriKind.Relative));

            // Assert.
            //the check of bee, not the status of a pin whose reference would be "check"
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            pinsHandlerMock.Verify(h => h.CheckPinsAsync(null, false, It.IsAny<HttpContext>()), Times.Once);
            pinsHandlerMock.Verify(h => h.GetPinStatusBeeAsync(It.IsAny<SwarmReference>()), Times.Never);
        }

        [Theory]
        [InlineData("dry=true", true)]
        [InlineData("dry=false", false)]
        [InlineData("", false)]
        public async Task CheckOfAReferenceTakesTheReferenceAndTheDryFlag(string dryQuery, bool expectedDry)
        {
            // Action.
            using var response = await client.GetAsync(new Uri($"/pins/check?ref={Reference}&{dryQuery}", UriKind.Relative));

            // Assert.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            pinsHandlerMock.Verify(h => h.CheckPinsAsync(Reference, expectedDry, It.IsAny<HttpContext>()), Times.Once);
        }

        [Theory]
        [InlineData("/pins/check?dry=maybe")]
        [InlineData("/pins/check?ref=not-a-reference")]
        public async Task CheckRefusesAValueItCannotRead(string pathAndQuery)
        {
            // Action.
            using var response = await client.GetAsync(new Uri(pathAndQuery, UriKind.Relative));

            // Assert.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            pinsHandlerMock.Verify(
                h => h.CheckPinsAsync(It.IsAny<SwarmReference?>(), It.IsAny<bool>(), It.IsAny<HttpContext>()),
                Times.Never);
        }

        [Theory]
        [InlineData("", null)]
        [InlineData("?failed=true", true)]
        [InlineData("?failed=false", false)]
        public async Task DeleteOfTheRunsTakesTheFailedFilter(string query, bool? expectedFailed)
        {
            // Action.
            using var response = await client.DeleteAsync(new Uri("/pins/check/runs" + query, UriKind.Relative));

            // Assert.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            beehiveHandlerMock.Verify(h => h.DeletePinsChecksAsync(expectedFailed, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("", null, 0, 500)]
        [InlineData("?outcome=failed&page=2&take=10", PinCheckOutcome.Failed, 2, 10)] //the outcome in the casing the api documents
        [InlineData("?outcome=Incomplete", PinCheckOutcome.Incomplete, 0, 500)]
        public async Task PinsOfARunTakeTheOutcomeAndThePage(
            string query,
            PinCheckOutcome? expectedOutcome,
            int expectedPage,
            int expectedTake)
        {
            // Action.
            using var response = await client.GetAsync(new Uri($"/pins/check/runs/{CheckId}/pins{query}", UriKind.Relative));

            // Assert.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            beehiveHandlerMock.Verify(
                h => h.GetPinsCheckPinsAsync(CheckId, expectedOutcome, expectedPage, expectedTake, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task PinsOfARunRefuseAnOutcomeThatIsNotOne()
        {
            // Action.
            using var response = await client.GetAsync(new Uri($"/pins/check/runs/{CheckId}/pins?outcome=whole", UriKind.Relative));

            // Assert.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            beehiveHandlerMock.Verify(
                h => h.GetPinsCheckPinsAsync(It.IsAny<string>(), It.IsAny<PinCheckOutcome?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task RunOfAnIdIsReadByItsId()
        {
            // Action.
            using var response = await client.GetAsync(new Uri($"/pins/check/runs/{CheckId}", UriKind.Relative));

            // Assert.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            beehiveHandlerMock.Verify(h => h.GetPinsCheckAsync(CheckId, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Theory]
        [InlineData("", 0, 100)]
        [InlineData("?page=3&take=7", 3, 7)]
        public async Task RunsAreListedByPage(string query, int expectedPage, int expectedTake)
        {
            // Action.
            using var response = await client.GetAsync(new Uri("/pins/check/runs" + query, UriKind.Relative));

            // Assert.
            //the list of the runs, not the status of a pin nor the check of every pin
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            beehiveHandlerMock.Verify(h => h.GetPinsChecksAsync(expectedPage, expectedTake, It.IsAny<CancellationToken>()), Times.Once);
            pinsHandlerMock.Verify(
                h => h.CheckPinsAsync(It.IsAny<SwarmReference?>(), It.IsAny<bool>(), It.IsAny<HttpContext>()),
                Times.Never);
        }

        [Fact]
        public async Task StartOfARunRefusesAStateThatIsNotOne()
        {
            // Action.
            using var response = await client.PostAsync(new Uri("/pins/check/runs?state=succeeded&state=whole", UriKind.Relative), null);

            // Assert.
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            beehiveHandlerMock.Verify(
                h => h.StartPinsCheckAsync(It.IsAny<ChunkPinState[]?>(), It.IsAny<PinsCheckInput?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task StartOfARunTakesTheReferencesOfItsBody()
        {
            // Action.
            using var response = await client.PostAsJsonAsync(
                new Uri("/pins/check/runs", UriKind.Relative),
                new { references = new[] { Reference.ToString() } });

            // Assert.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            beehiveHandlerMock.Verify(
                h => h.StartPinsCheckAsync(
                    It.Is<ChunkPinState[]?>(s => s == null || s.Length == 0),
                    It.Is<PinsCheckInput?>(i => i != null && i.References.SequenceEqual(new[] { Reference.ToString() })),
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task StartOfARunTakesTheStatesInAnyCasingOncePerState()
        {
            // Action.
            using var response = await client.PostAsync(
                new Uri("/pins/check/runs?state=incomplete&state=UNPROCESSED", UriKind.Relative),
                null);

            // Assert.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            beehiveHandlerMock.Verify(
                h => h.StartPinsCheckAsync(
                    It.Is<ChunkPinState[]?>(s => s != null && s.SequenceEqual(new[] { ChunkPinState.Incomplete, ChunkPinState.Unprocessed })),
                    null,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task StartOfARunWithoutStatesNorBodyTakesEveryPin()
        {
            // Action.
            using var response = await client.PostAsync(new Uri("/pins/check/runs", UriKind.Relative), null);

            // Assert.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            beehiveHandlerMock.Verify(
                h => h.StartPinsCheckAsync(
                    It.Is<ChunkPinState[]?>(s => s == null || s.Length == 0),
                    null,
                    It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task StopOfARunIsAskedByItsId()
        {
            // Action.
            using var response = await client.PostAsync(new Uri($"/pins/check/runs/{CheckId}/stop", UriKind.Relative), null);

            // Assert.
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            beehiveHandlerMock.Verify(h => h.StopPinsCheckAsync(CheckId, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}
