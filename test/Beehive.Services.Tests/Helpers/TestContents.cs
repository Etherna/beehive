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

using Etherna.SwarmSdk.Hashing;
using Etherna.SwarmSdk.Hashing.Pipeline;
using Etherna.SwarmSdk.Hashing.Postage;
using Etherna.SwarmSdk.Hashing.Signer;
using Etherna.SwarmSdk.Models;
using Etherna.SwarmSdk.Stores;
using System.Linq;
using System.Threading.Tasks;

namespace Etherna.Beehive.Services.Helpers
{
    /// <summary>
    /// The contents the pin tests work on, built as an upload would build them
    /// </summary>
    internal static class TestContents
    {
        // Consts.
        //three levels: 150 data chunks under 2 intermediate chunks under the root
        public const int ContentLength = 600 * 1024;
        private const string PrivateKeyHex = "0x4f3edf983ac636a65a842ce7c78d9aa706d3b113bce9c46f30d7d21715b23b1d";

        // Methods.
        public static SwarmSoc NewSignedSoc()
        {
            var bmt = new SwarmChunkBmt();
            var spanData = SwarmCac.BuildFromData(SwarmHash.Zero, Enumerable.Range(0, 42).Select(i => (byte)i).ToArray()).SpanData;
            var innerChunk = new SwarmCac(bmt.Hash(spanData), spanData);
            var signer = new PrivateKeySigner(new EthPrivateKey(PrivateKeyHex));
            SwarmSocIdentifier identifier = Enumerable.Range(0, SwarmSocIdentifier.IdentifierSize).Select(i => (byte)i).ToArray();
            var soc = new SwarmSoc(identifier, signer.PublicAddress, innerChunk);
            soc.Sign(signer, new Hasher());
            return soc;
        }

        /// <summary>
        /// Build a content with the sdk hasher pipeline into the given store, as an upload would: deterministic
        /// pseudo-random data, so that no two data chunks are the same
        /// </summary>
        public static async Task<SwarmReference> UploadContentAsync(IChunkStore chunkStore)
        {
            var data = new byte[ContentLength];
            var state = 2463534242u; //xorshift32
            for (var i = 0; i < data.Length; i++)
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                data[i] = (byte)state;
            }

            using var hasherPipeline = HasherPipelineBuilder.BuildNewHasherPipeline(
                chunkStore,
                new FakePostageStamper(),
                RedundancyLevel.None,
                false,
                0,
                null);
            return await hasherPipeline.HashDataAsync(data);
        }
    }
}
