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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using Xunit;

namespace Etherna.Beehive.Services.Extensions
{
    public class ExceptionExtensionsTest
    {
        // Test data.
        public static IEnumerable<object[]> BeeNodeFailureReasonTests
        {
            get
            {
                var tests = new List<(Exception Exception, string ExpectedReason)>
                {
                    //the node answered bee's error object: its message
                    (new SwarmSdkApiException(
                        "The HTTP status code of the response was not expected (503).",
                        503,
                        """{"code":503,"message":"syncing in progress"}""",
                        new Dictionary<string, IEnumerable<string>>(),
                        null),
                        "syncing in progress"),

                    //the node answered something else: the first line of the sdk message
                    (new SwarmSdkApiException(
                        "The HTTP status code of the response was not expected (502).",
                        502,
                        "<html>Bad Gateway</html>",
                        new Dictionary<string, IEnumerable<string>>(),
                        null),
                        "The HTTP status code of the response was not expected (502)."),

                    //no body, as the dev node client does: the first line of the message, without the status dump
                    (new SwarmSdkApiException(
                        "Postage batch not found",
                        404,
                        null,
                        new Dictionary<string, IEnumerable<string>>(),
                        null),
                        "Postage batch not found"),

                    //the node did not answer: the exception message
                    (new HttpRequestException("Connection refused (bee1:1633)"), "Connection refused (bee1:1633)")
                };

                return tests.Select(t => new object[] { t.Exception, t.ExpectedReason });
            }
        }

        // Tests.

        [Theory, MemberData(nameof(BeeNodeFailureReasonTests))]
        public void GetBeeNodeFailureReasonReportsTheNodeReason(Exception exception, string expectedReason)
        {
            // Action.
            var reason = exception.GetBeeNodeFailureReason();

            // Assert.
            Assert.Equal(expectedReason, reason);
        }
    }
}
