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

using Microsoft.AspNetCore.Http;
using Xunit;

namespace Etherna.Beehive.Extensions
{
    public class HeaderDictionaryExtensionsTest
    {
        // Consts.
        private const string ExposeHeadersName = "Access-Control-Expose-Headers";

        // Tests.

        [Fact]
        public void ExposeHeadersAddsToEmptyList()
        {
            // Setup.
            var headers = new HeaderDictionary();

            // Action.
            headers.ExposeHeaders("Swarm-Feed-Index", "Swarm-Soc-Signature");

            // Assert.
            Assert.Equal("Swarm-Feed-Index,Swarm-Soc-Signature", headers[ExposeHeadersName].ToString());
        }

        [Fact]
        public void ExposeHeadersIsAdditiveAcrossCalls()
        {
            // Setup.
            var headers = new HeaderDictionary();

            // Action.
            headers.ExposeHeaders("Swarm-Feed-Index");
            headers.ExposeHeaders("Content-Disposition");

            // Assert.
            Assert.Equal("Swarm-Feed-Index,Content-Disposition", headers[ExposeHeadersName].ToString());
        }

        [Fact]
        public void ExposeHeadersMergesCommaJoinedExistingValue()
        {
            // Setup.
            var headers = new HeaderDictionary
            {
                { ExposeHeadersName, "Swarm-Feed-Index, Swarm-Soc-Signature" }
            };

            // Action.
            headers.ExposeHeaders("Content-Disposition");

            // Assert.
            Assert.Equal(
                "Swarm-Feed-Index,Swarm-Soc-Signature,Content-Disposition",
                headers[ExposeHeadersName].ToString());
        }

        [Fact]
        public void ExposeHeadersDeduplicatesCaseInsensitively()
        {
            // Setup.
            var headers = new HeaderDictionary();

            // Action.
            headers.ExposeHeaders("Swarm-Feed-Index");
            headers.ExposeHeaders("swarm-feed-index", "Swarm-Feed-Index");

            // Assert.
            Assert.Equal("Swarm-Feed-Index", headers[ExposeHeadersName].ToString());
        }
    }
}
