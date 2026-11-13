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

using Etherna.MongoDB.Bson;
using Etherna.MongoDB.Driver;
using Etherna.MongoDB.Driver.Core.Clusters;
using Etherna.MongoDB.Driver.Core.Connections;
using Etherna.MongoDB.Driver.Core.Servers;
using Serilog;
using Serilog.Core;
using Serilog.Events;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Xunit;
using OfficialBson = MongoDB.Bson;
using OfficialDriver = MongoDB.Driver;

namespace Etherna.Beehive.Extensions
{
    public class LoggerEnrichmentConfigurationExtensionsTest
    {
        // Test data.
        public static TheoryData<MongoException, string> DriverObjectGraphs()
        {
            var connectionId = BuildConnectionId();
            var queryException = new MongoQueryException(
                connectionId,
                "Query failed",
                new BsonDocument("find", "chunks"),
                new BsonDocument { { "ok", 0 }, { "errmsg", "cursor killed" } });
            var writeConcernException = new MongoWriteConcernException(
                connectionId,
                "Write concern not satisfied",
                new WriteConcernResult(new BsonDocument { { "ok", 1 }, { "n", 1 }, { "writeConcernError", new BsonDocument("errmsg", "timeout") } }));

            return new TheoryData<MongoException, string>
            {
                { queryException, "Query" },
                { queryException, "QueryResult" },
                { writeConcernException, "WriteConcernResult" },
                { writeConcernException, "Command" }
            };
        }

        // Tests.

        [Fact]
        public void MongoCommandExceptionKeepsScalarsAndDropsDocuments()
        {
            // Setup.
            //the shape of a real command: a string element beside a document element
            var command = new BsonDocument
            {
                { "update", "chunkPins" },
                { "updates", new BsonArray { new BsonDocument("q", new BsonDocument("_id", ObjectId.GenerateNewId())) } }
            };
            var result = new BsonDocument
            {
                { "ok", 0 },
                { "errmsg", "WriteConflict error" },
                { "code", 112 },
                { "codeName", "WriteConflict" },
                { "errorLabels", new BsonArray { "TransientTransactionError" } }
            };
            var exception = new MongoCommandException(BuildConnectionId(), "Command update failed: WriteConflict error", command, result);

            // Action.
            var details = EnrichExceptionDetails(exception);

            // Assert.
            Assert.Equal(112, ScalarOf(details, "Code"));
            Assert.Equal("WriteConflict", ScalarOf(details, "CodeName"));
            Assert.Equal("WriteConflict error", ScalarOf(details, "ErrorMessage"));
            Assert.Equal(["TransientTransactionError"], SequenceOf(details, "ErrorLabels"));
            Assert.Equal(exception.Message, ScalarOf(details, "Message"));
            Assert.DoesNotContain("Command", details.Keys);
            Assert.DoesNotContain("Result", details.Keys);
            Assert.DoesNotContain("ConnectionId", details.Keys);
        }

        [Fact]
        public void MongoConnectionExceptionKeepsInnerException()
        {
            // Setup.
            var exception = new MongoConnectionException(
                BuildConnectionId(),
                "An exception occurred while receiving a message from the server",
                new SocketException((int)SocketError.ConnectionReset));

            // Action.
            var details = EnrichExceptionDetails(exception);

            // Assert.
            //the inner exception is inherited from System.Exception: not a driver graph
            Assert.Equal("System.Net.Sockets.SocketException", ScalarOf(DictionaryOf(details, "InnerException"), "Type"));
            Assert.Equal(true, ScalarOf(details, "IsNetworkException"));
            Assert.DoesNotContain("ConnectionId", details.Keys);
        }

        [Theory]
        [MemberData(nameof(DriverObjectGraphs))]
        public void MongoExceptionDropsEveryDriverObjectGraph(MongoException exception, string graphProperty)
        {
            ArgumentNullException.ThrowIfNull(exception);

            // Setup.
            //the property must exist, or the assert below would pass on a typo
            Assert.NotNull(exception.GetType().GetProperty(graphProperty));

            // Action.
            var details = EnrichExceptionDetails(exception);

            // Assert.
            Assert.DoesNotContain(graphProperty, details.Keys);
        }

        [Fact]
        public void OfficialDriverCommandExceptionKeepsScalarsAndDropsDocuments()
        {
            // Setup.
            //Hangfire.Mongo throws the official driver's exceptions, not the Etherna fork's Scrinium throws:
            //the job queue watcher failing on a standalone mongod, as logged by Hangfire
            var command = new OfficialBson.BsonDocument
            {
                { "aggregate", "hangfire.jobGraph" },
                { "pipeline", new OfficialBson.BsonArray { new OfficialBson.BsonDocument("$changeStream", new OfficialBson.BsonDocument()) } }
            };
            var result = new OfficialBson.BsonDocument
            {
                { "ok", 0 },
                { "errmsg", "The $changeStream stage is only supported on replica sets or mongos" },
                { "code", 40573 },
                { "codeName", "Location40573" }
            };
            var exception = new OfficialDriver.MongoCommandException(
                new OfficialDriver.Core.Connections.ConnectionId(
                    new OfficialDriver.Core.Servers.ServerId(new OfficialDriver.Core.Clusters.ClusterId(), new DnsEndPoint("localhost", 27017))),
                "Command aggregate failed: The $changeStream stage is only supported on replica sets or mongos.",
                command,
                result);

            // Action.
            var details = EnrichExceptionDetails(exception);

            // Assert.
            Assert.Equal(40573, ScalarOf(details, "Code"));
            Assert.Equal("Location40573", ScalarOf(details, "CodeName"));
            Assert.Equal("The $changeStream stage is only supported on replica sets or mongos", ScalarOf(details, "ErrorMessage"));
            Assert.Equal(exception.Message, ScalarOf(details, "Message"));
            Assert.DoesNotContain("Command", details.Keys);
            Assert.DoesNotContain("Result", details.Keys);
            Assert.DoesNotContain("ConnectionId", details.Keys);
        }

        [Fact]
        public void OtherExceptionKeepsObjectProperties()
        {
            // Setup.
            var exception = new ValidationException(
                new ValidationResult("Reference is required", ["Reference"]),
                validatingAttribute: null,
                value: null);

            // Action.
            var details = EnrichExceptionDetails(exception);

            // Assert.
            Assert.Equal("Reference is required", ScalarOf(DictionaryOf(details, "ValidationResult"), "ErrorMessage"));
        }

        // Helpers.
        private static ConnectionId BuildConnectionId() =>
            new(new ServerId(new ClusterId(), new DnsEndPoint("localhost", 27017)));

        private static Dictionary<string, LogEventPropertyValue> DictionaryOf(Dictionary<string, LogEventPropertyValue> details, string propertyName) =>
            ToDictionary(Assert.IsType<DictionaryValue>(details[propertyName]));

        private static Dictionary<string, LogEventPropertyValue> EnrichExceptionDetails(Exception exception)
        {
            var sink = new ListSink();
            using var logger = new LoggerConfiguration()
                .Enrich.WithBeehiveExceptionDetails()
                .WriteTo.Sink(sink)
                .CreateLogger();

            logger.Error(exception, "Probe");

            var logEvent = Assert.Single(sink.Events);
            return ToDictionary(Assert.IsType<DictionaryValue>(logEvent.Properties["ExceptionDetail"]));
        }

        private static object? ScalarOf(Dictionary<string, LogEventPropertyValue> details, string propertyName) =>
            Assert.IsType<ScalarValue>(details[propertyName]).Value;

        private static IEnumerable<object?> SequenceOf(Dictionary<string, LogEventPropertyValue> details, string propertyName) =>
            Assert.IsType<SequenceValue>(details[propertyName]).Elements.Select(e => Assert.IsType<ScalarValue>(e).Value);

        private static Dictionary<string, LogEventPropertyValue> ToDictionary(DictionaryValue dictionary) =>
            dictionary.Elements.ToDictionary(e => Assert.IsType<string>(e.Key.Value), e => e.Value);

        private sealed class ListSink : ILogEventSink
        {
            public List<LogEvent> Events { get; } = [];

            public void Emit(LogEvent logEvent) => Events.Add(logEvent);
        }
    }
}
