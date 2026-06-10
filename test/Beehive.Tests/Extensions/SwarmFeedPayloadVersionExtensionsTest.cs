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

using Etherna.SwarmSdk.Models;
using Xunit;

namespace Etherna.Beehive.Extensions
{
    public class SwarmFeedPayloadVersionExtensionsTest
    {
        // Tests.

        [Theory]
        [InlineData(SwarmFeedPayloadVersion.V1, "v1")]
        [InlineData(SwarmFeedPayloadVersion.V2, "v2")]
        public void ToHeaderValueMapsToBeeWireValue(SwarmFeedPayloadVersion version, string expected)
        {
            // Action.
            var result = version.ToHeaderValue();

            // Assert.
            Assert.Equal(expected, result);
        }
    }
}
