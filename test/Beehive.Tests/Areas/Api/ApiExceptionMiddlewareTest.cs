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
using Etherna.SwarmSdk.Exceptions;
using Etherna.SwarmSdk.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Net.Http.Headers;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Etherna.Beehive.Areas.Api
{
    [Collection("Static Serilog logger")] //both classes swap Log.Logger: run them one at a time
    public class ApiExceptionMiddlewareTest
    {
        // Tests.

        [Fact]
        public async Task AbortsTheConnectionWhenTheResponseHasStarted()
        {
            // Setup.
            //the body was being written when a chunk failed: the client already has part of a 200
            var context = NewContext(new SwarmApiMarker());
            context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
            var lifetime = new RecordingLifetimeFeature();
            context.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
            var exception = NewChunksNotFoundException();
            var middleware = new ApiExceptionMiddleware(_ => throw exception);
            var sink = new ListSink();
            var originalLogger = Log.Logger;
            Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
            try
            {
                // Action.
                await middleware.InvokeAsync(context);

                // Assert.
                Assert.True(lifetime.Aborted);
                var logEvent = Assert.Single(sink.Events);
                Assert.Equal(LogEventLevel.Warning, logEvent.Level); //a 404 answers at debug; a body cut short is a warning
                Assert.Same(exception, logEvent.Exception);
                Assert.Equal(StatusCodes.Status404NotFound, Assert.IsType<ScalarValue>(logEvent.Properties["StatusCode"]).Value);
                Assert.Contains("/bytes/abc", logEvent.RenderMessage(CultureInfo.InvariantCulture), StringComparison.Ordinal);
            }
            finally
            {
                Log.Logger = originalLogger;
            }
        }

        [Fact]
        public async Task AnswersTheBeehiveApiInItsOwnBodyShape()
        {
            // Setup.
            var context = NewContext(new BeehiveApiMarker());
            var middleware = new ApiExceptionMiddleware(_ => throw new KeyNotFoundException());

            // Action.
            await middleware.InvokeAsync(context);

            // Assert.
            //the message as a json string, the body of the etherna services
            Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
            Assert.Equal("Not found", JsonDocument.Parse(ReadBody(context)).RootElement.GetString());
        }

        [Fact]
        public async Task AnswersTheMappedResultWhenTheResponseHasNotStarted()
        {
            // Setup.
            //the result of the endpoint set its status and headers, then failed before writing: a chunk missing at the first read of the body
            var context = NewContext(new SwarmApiMarker());
            var exception = NewChunksNotFoundException();
            var middleware = new ApiExceptionMiddleware(ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status200OK;
                ctx.Response.Headers.AcceptRanges = "bytes";
                throw exception;
            });
            var sink = new ListSink();
            var originalLogger = Log.Logger;
            Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
            try
            {
                // Action.
                await middleware.InvokeAsync(context);

                // Assert.
                Assert.Equal(StatusCodes.Status404NotFound, context.Response.StatusCode);
                Assert.DoesNotContain(HeaderNames.AcceptRanges, context.Response.Headers.Keys); //the replaced response left nothing
                var body = JsonDocument.Parse(ReadBody(context)).RootElement;
                Assert.Equal(404, body.GetProperty("code").GetInt32());
                Assert.Equal(exception.Message, body.GetProperty("message").GetString());
                var logEvent = Assert.Single(sink.Events);
                Assert.Equal(LogEventLevel.Debug, logEvent.Level); //as a 404 answered by the handler
                Assert.Same(exception, logEvent.Exception);
            }
            finally
            {
                Log.Logger = originalLogger;
            }
        }

        [Fact]
        public async Task PassesTheExceptionThroughOutsideTheApi()
        {
            // Setup.
            //an endpoint without an api marker, as the pages
            var context = NewContext(null);
            var exception = new InvalidOperationException();
            var middleware = new ApiExceptionMiddleware(_ => throw exception);

            // Action & assert.
            Assert.Same(exception, await Assert.ThrowsAsync<InvalidOperationException>(() => middleware.InvokeAsync(context)));
            Assert.Equal(StatusCodes.Status200OK, context.Response.StatusCode);
            Assert.Empty(ReadBody(context));
        }

        [Fact]
        public async Task PassesTheExceptionThroughWhenTheClientIsGone()
        {
            // Setup.
            //the client went away and the read it was waiting for was cancelled: nothing to answer, nothing to log
            var context = NewContext(new SwarmApiMarker());
            var lifetime = new RecordingLifetimeFeature { RequestAborted = new CancellationToken(canceled: true) };
            context.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
            var exception = new OperationCanceledException();
            var middleware = new ApiExceptionMiddleware(_ => throw exception);
            var sink = new ListSink();
            var originalLogger = Log.Logger;
            Log.Logger = new LoggerConfiguration().MinimumLevel.Verbose().WriteTo.Sink(sink).CreateLogger();
            try
            {
                // Action & assert.
                Assert.Same(exception, await Assert.ThrowsAsync<OperationCanceledException>(() => middleware.InvokeAsync(context)));
                Assert.False(lifetime.Aborted);
                Assert.Empty(sink.Events);
            }
            finally
            {
                Log.Logger = originalLogger;
            }
        }

        // Helpers.
        private static DefaultHttpContext NewContext(object? apiMarker)
        {
            var context = new DefaultHttpContext
            {
                RequestServices = new ServiceCollection().AddLogging().BuildServiceProvider()
            };
            context.Request.Path = "/bytes/abc";
            context.Response.Body = new MemoryStream();
            object[] metadata = apiMarker is null ? [] : [apiMarker];
            context.SetEndpoint(new Endpoint(null, new EndpointMetadataCollection(metadata), "test"));
            return context;
        }

        /// <summary>
        /// The sdk's not found of a read: a child of the root chunk could not be fetched
        /// </summary>
        private static SwarmChunkChildrenNotFoundException NewChunksNotFoundException() =>
            new(
                new SwarmReference(SwarmHash.Zero, null),
                new SwarmChunksNotFoundException([new SwarmShardReference(
                    new SwarmReference("2222222222222222222222222222222222222222222222222222222222222222", null),
                    false)]));

        private static byte[] ReadBody(HttpContext context) => ((MemoryStream)context.Response.Body).ToArray();

        private sealed class ListSink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = [];

            public void Emit(LogEvent logEvent) => Events.Add(logEvent);
        }

        private sealed class RecordingLifetimeFeature : IHttpRequestLifetimeFeature
        {
            public bool Aborted { get; private set; }
            public CancellationToken RequestAborted { get; set; }

            public void Abort() => Aborted = true;
        }

        /// <summary>
        /// A response whose first bytes went out already
        /// </summary>
        private sealed class StartedResponseFeature : HttpResponseFeature
        {
            public override bool HasStarted => true;
        }
    }
}
