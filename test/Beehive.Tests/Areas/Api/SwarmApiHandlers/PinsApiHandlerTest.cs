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
using Etherna.Beehive.Services.Domain;
using Etherna.Beehive.Services.Tasks.Trigger;
using Etherna.Scrinium.Core;
using Etherna.Scrinium.Core.Repositories;
using Etherna.SwarmSdk.Models;
using Hangfire;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Areas.Api.SwarmApiHandlers
{
    public class PinsApiHandlerTest
    {
        // Consts.
        private static readonly SwarmReference Reference = "aa00000000000000000000000000000000000000000000000000000000000001";

        // Fields.
        private readonly List<ChunkPin> chunkPins = [];
        private readonly PinsApiHandler handler;
        private readonly Mock<IPinChunksTask> pinChunksTaskMock = new();

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
            var dbContextMock = new Mock<IBeehiveDbContext>();
            dbContextMock.Setup(c => c.ChunkPins).Returns(chunkPinsMock.Object);

            // The pinning itself is the task's: here every pin succeeds.
            pinChunksTaskMock.Setup(t => t.RunAsync(It.IsAny<string>()))
                .Callback((string pinId) => chunkPins.Single(p => p.Id == pinId).UpdateProcessed([], 3))
                .Returns(Task.CompletedTask);

            handler = new PinsApiHandler(
                new Mock<IBackgroundJobClient>().Object,
                dbContextMock.Object,
                pinChunksTaskMock.Object,
                new Mock<IPinService>().Object);
        }

        // Tests.

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
            pinChunksTaskMock.Verify(t => t.RunAsync(It.IsAny<string>()), Times.Never); //nothing to pin again
        }

        // Helpers.
        private static async Task<(int StatusCode, string? ContentType, string Body)> ExecuteResultAsync(IResult result)
        {
            var httpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
            };
            await using var bodyStream = new MemoryStream();
            httpContext.Response.Body = bodyStream;

            await result.ExecuteAsync(httpContext);

            return (
                httpContext.Response.StatusCode,
                httpContext.Response.ContentType,
                Encoding.UTF8.GetString(bodyStream.ToArray()));
        }
    }
}
