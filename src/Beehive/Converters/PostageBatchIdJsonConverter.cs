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

using Etherna.BeeNet.Models;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Etherna.Beehive.Converters
{
    public class PostageBatchIdJsonConverter : JsonConverter<PostageBatchId>
    {
        public override PostageBatchId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException();

            return reader.GetString()!;
        }

        public override void Write(Utf8JsonWriter writer, PostageBatchId value, JsonSerializerOptions options)
        {
            ArgumentNullException.ThrowIfNull(writer, nameof(writer));
            
            writer.WriteStringValue(value.ToString());
        }
    }
}