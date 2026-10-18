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

using Etherna.Beehive.Configs;
using Etherna.SwarmSdk.Models;
using System;
using System.Text.Json;
using Xunit;

namespace Etherna.Beehive.Areas.Api.DtoModels
{
    public class PostageBatchDtoTest
    {
        // Consts.
        private static readonly PostageBatchId BatchId = "c1845f41a7935a0366c6475974f8875ea569ade8aec5d34e856f2c88c0b0695f";

        // Tests.

        [Fact]
        public void DegradedBatchOmitsUnknownValuesAndWritesError()
        {
            // Setup.
            var dto = new PostageBatchDto(
                amount: null,
                batchId: BatchId,
                batchTtl: null,
                blockNumber: null,
                bucketDepth: PostageBatch.BucketDepth,
                depth: 20,
                error: "Node is not ready",
                exists: null,
                immutableFlag: true,
                label: "",
                usable: false,
                utilization: 7);

            // Action.
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, CommonConsts.SwarmJsonSerializerOptions));
            var root = json.RootElement;

            // Assert.
            Assert.Equal(BatchId.ToString(), root.GetProperty("batchID").GetString());
            Assert.Equal(20, root.GetProperty("depth").GetInt32());
            Assert.Equal("Node is not ready", root.GetProperty("error").GetString());
            Assert.True(root.GetProperty("immutableFlag").GetBoolean());
            Assert.False(root.GetProperty("usable").GetBoolean());
            Assert.Equal(7, root.GetProperty("utilization").GetInt32());
            Assert.Equal(JsonValueKind.Null, root.GetProperty("amount").ValueKind);
            //the SwarmSdk generated client reads these as non-nullable value types: absent, never null
            Assert.False(root.TryGetProperty("batchTTL", out _));
            Assert.False(root.TryGetProperty("blockNumber", out _));
            Assert.False(root.TryGetProperty("exists", out _));
        }

        [Fact]
        public void LiveBatchKeepsBeeShapeWithoutError()
        {
            // Setup.
            var dto = new PostageBatchDto(
                amount: BzzValue.FromPlurDecimal(2831120640),
                batchId: BatchId,
                batchTtl: TimeSpan.FromSeconds(120105),
                blockNumber: 44667643,
                bucketDepth: PostageBatch.BucketDepth,
                depth: 17,
                error: null,
                exists: true,
                immutableFlag: false,
                label: "my batch",
                usable: true,
                utilization: 0);

            // Action.
            using var json = JsonDocument.Parse(JsonSerializer.Serialize(dto, CommonConsts.SwarmJsonSerializerOptions));
            var root = json.RootElement;

            // Assert.
            Assert.Equal("2831120640", root.GetProperty("amount").GetString());
            Assert.Equal(120105, root.GetProperty("batchTTL").GetInt64());
            Assert.Equal(44667643ul, root.GetProperty("blockNumber").GetUInt64());
            Assert.True(root.GetProperty("exists").GetBoolean());
            Assert.True(root.GetProperty("usable").GetBoolean());
            Assert.False(root.TryGetProperty("error", out _));
        }
    }
}
