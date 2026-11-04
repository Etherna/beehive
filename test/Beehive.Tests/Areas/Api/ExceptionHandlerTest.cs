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
using Etherna.MongoDB.Bson;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.Core.Clusters;
using Etherna.MongoDB.Driver.Core.Connections;
using Etherna.MongoDB.Driver.Core.Servers;
using Etherna.SwarmSdk.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Areas.Api
{
    public class ExceptionHandlerTest
    {
        // Test data.
        public static IEnumerable<object[]> NodeCallFailureTests
        {
            get
            {
                var tests = new List<(Exception Exception, int ExpectedStatusCode, string ExpectedMessage)>
                {
                    //the node refused the connection
                    (new HttpRequestException("Connection refused (node:1633)"),
                        StatusCodes.Status503ServiceUnavailable, "Connection refused (node:1633)"),

                    //the node did not complete the call within the bound of its operation
                    (new SwarmClientTimeoutException("SwarmClient.GetChunkAsync", TimeSpan.FromSeconds(120), null),
                        StatusCodes.Status504GatewayTimeout, "SwarmClient.GetChunkAsync did not complete within 120 s")
                };

                return tests.Select(t => new object[] { t.Exception, t.ExpectedStatusCode, t.ExpectedMessage });
            }
        }

        public static IEnumerable<object[]> PostageBatchVerificationTests
        {
            get
            {
                var tests = new List<(Exception Exception, int ExpectedStatusCode, string ExpectedMessage)>
                {
                    //bee's own upload answers, whatever the internal message says
                    (new PostageBatchNotFoundException("batch x not found on node y"),
                        StatusCodes.Status404NotFound, "batch with id not found"),
                    (new PostageBatchNotUsableException("batch x not usable yet on node y"),
                        StatusCodes.Status422UnprocessableEntity, "batch not usable yet or does not exist"),

                    //the node's reason
                    (new BeeNodeUnavailableException("Node is not ready"),
                        StatusCodes.Status503ServiceUnavailable, "Node is not ready")
                };

                return tests.Select(t => new object[] { t.Exception, t.ExpectedStatusCode, t.ExpectedMessage });
            }
        }

        // Tests.

        [Theory]
        [InlineData(typeof(KeyNotFoundException), StatusCodes.Status404NotFound, LogEventLevel.Debug)]
        [InlineData(typeof(BeeNodeUnavailableException), StatusCodes.Status503ServiceUnavailable, LogEventLevel.Warning)]
        [InlineData(typeof(InvalidOperationException), StatusCodes.Status500InternalServerError, LogEventLevel.Error)]
        public async Task LogsTheExceptionAtTheLevelOfTheAnsweredStatus(
            Type exceptionType,
            int expectedStatusCode,
            LogEventLevel expectedLevel)
        {
            // Setup.
            //the handler writes on the static Serilog logger: capture it for this test, then restore it
            var exception = (Exception)Activator.CreateInstance(exceptionType)!;
            var sink = new ListSink();
            var originalLogger = Log.Logger;
            Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
            try
            {
                // Action.
                var result = await ExceptionHandler.RunAsync(ApiVersion.Swarm, () => throw exception);
                var (statusCode, _) = await ExecuteResultAsync(result);

                // Assert.
                Assert.Equal(expectedStatusCode, statusCode);
                var logEvent = Assert.Single(sink.Events);
                Assert.Equal(expectedLevel, logEvent.Level);
                Assert.Same(exception, logEvent.Exception);
                Assert.Equal(expectedStatusCode, Assert.IsType<ScalarValue>(logEvent.Properties["StatusCode"]).Value);
            }
            finally
            {
                Log.Logger = originalLogger;
            }
        }

        [Theory, MemberData(nameof(NodeCallFailureTests))]
        public async Task NodeCallFailureAnswersNodeFailureStatusWithReasonAtWarning(
            Exception exception,
            int expectedStatusCode,
            string expectedMessage)
        {
            // Setup.
            var sink = new ListSink();
            var originalLogger = Log.Logger;
            Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
            try
            {
                // Action.
                var result = await ExceptionHandler.RunAsync(ApiVersion.Swarm, () => throw exception);
                var (statusCode, body) = await ExecuteResultAsync(result);

                // Assert.
                Assert.Equal(expectedStatusCode, statusCode);
                Assert.Equal(expectedStatusCode, body.GetProperty("code").GetInt32());
                Assert.Equal(expectedMessage, body.GetProperty("message").GetString());
                var logEvent = Assert.Single(sink.Events);
                Assert.Equal(LogEventLevel.Warning, logEvent.Level);
                Assert.Same(exception, logEvent.Exception);
            }
            finally
            {
                Log.Logger = originalLogger;
            }
        }

        [Theory, MemberData(nameof(PostageBatchVerificationTests))]
        public async Task PostageBatchVerificationAnswersBeeStatusAndMessage(
            Exception exception,
            int expectedStatusCode,
            string expectedMessage)
        {
            // Action.
            var result = await ExceptionHandler.RunAsync(ApiVersion.Swarm, () => throw exception);
            var (statusCode, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(expectedStatusCode, statusCode);
            Assert.Equal(expectedStatusCode, body.GetProperty("code").GetInt32());
            Assert.Equal(expectedMessage, body.GetProperty("message").GetString());
        }

        [Fact]
        public async Task SwarmSdkApiExceptionAnswersServiceUnavailableWithNodeReason()
        {
            // Setup.
            var exception = new SwarmSdkApiException(
                "The HTTP status code of the response was not expected (503).",
                503,
                """{"code":503,"message":"syncing in progress"}""",
                new Dictionary<string, IEnumerable<string>>(),
                null);

            // Action.
            var result = await ExceptionHandler.RunAsync(ApiVersion.Swarm, () => throw exception);
            var (statusCode, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, statusCode);
            Assert.Equal(503, body.GetProperty("code").GetInt32());
            Assert.Equal("syncing in progress", body.GetProperty("message").GetString());
        }

        [Theory]
        [InlineData(ApiVersion.BeehiveV04)]
        [InlineData(ApiVersion.Swarm)]
        public async Task TransientWriteConflictAnswersConflictOnEveryApi(ApiVersion apiVersion)
        {
            // Setup.
            //a transaction that lost a write conflict with a concurrent one: the server asks the client to retry
            var exception = BuildWriteConflictException();
            exception.AddErrorLabel("TransientTransactionError");

            // Action.
            var result = await ExceptionHandler.RunAsync(apiVersion, () => throw exception);
            var (statusCode, body) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status409Conflict, statusCode);
            if (apiVersion == ApiVersion.Swarm)
            {
                Assert.Equal(409, body.GetProperty("code").GetInt32());
                Assert.Equal("Concurrent write conflict, retry the request", body.GetProperty("message").GetString());
            }
        }

        [Fact]
        public async Task TransientWriteConflictIsLoggedAtWarning()
        {
            // Setup.
            var exception = BuildWriteConflictException();
            exception.AddErrorLabel("TransientTransactionError");
            var sink = new ListSink();
            var originalLogger = Log.Logger;
            Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
            try
            {
                // Action.
                await ExceptionHandler.RunAsync(ApiVersion.Swarm, () => throw exception);

                // Assert.
                var logEvent = Assert.Single(sink.Events);
                Assert.Equal(LogEventLevel.Warning, logEvent.Level);
                Assert.Same(exception, logEvent.Exception);
            }
            finally
            {
                Log.Logger = originalLogger;
            }
        }

        [Fact]
        public async Task UnlabeledDriverExceptionAnswersInternalServerError()
        {
            // Setup.
            //the same driver exception without the transient label is not a retryable conflict
            var exception = BuildWriteConflictException();

            // Action.
            var result = await ExceptionHandler.RunAsync(ApiVersion.Swarm, () => throw exception);
            var (statusCode, _) = await ExecuteResultAsync(result);

            // Assert.
            Assert.Equal(StatusCodes.Status500InternalServerError, statusCode);
        }

        // Helpers.
        private static MongoCommandException BuildWriteConflictException()
        {
            var connectionId = new ConnectionId(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017)));
            var command = new BsonDocument("findAndModify", "chunkPins");
            var result = new BsonDocument
            {
                { "ok", 0 },
                { "errmsg", "Write conflict during plan execution and yielding is disabled" },
                { "code", 112 },
                { "codeName", "WriteConflict" }
            };
            return new MongoCommandException(connectionId, "Command findAndModify failed", command, result);
        }

        private sealed class ListSink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = [];

            public void Emit(LogEvent logEvent) => Events.Add(logEvent);
        }

        private static async Task<(int StatusCode, JsonElement Body)> ExecuteResultAsync(IResult result)
        {
            var httpContext = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
            };
            await using var bodyStream = new MemoryStream();
            httpContext.Response.Body = bodyStream;

            await result.ExecuteAsync(httpContext);

            bodyStream.Position = 0;
            using var body = await JsonDocument.ParseAsync(bodyStream);
            return (httpContext.Response.StatusCode, body.RootElement.Clone());
        }
    }
}
