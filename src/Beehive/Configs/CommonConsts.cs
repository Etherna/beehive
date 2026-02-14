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

using Etherna.Beehive.JsonConverters;
using Etherna.BeeNet.JsonConverters;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Etherna.Beehive.Configs
{
    internal static class CommonConsts
    {
        public const string DatabaseAdminPath = "/admin/db";
        public const string HangfireAdminPath = "/admin/hangfire";
        public const string SwaggerPath = "/swagger";
        
        public static readonly JsonSerializerOptions BeehiveV04JsonSerializerOptions = new()
        {
            Converters =
            {
                new BzzValueJsonConverter(true),
                new EncryptionKey256JsonConverter(),
                new EthAddressJsonConverter(),
                new JsonStringEnumConverter(),
                new PostageBatchIdJsonConverter(),
                new SwarmHashJsonConverter(),
                new SwarmReferenceJsonConverter(),
                new TimeSpanAsSecondsJsonConverter(),
                new XDaiValueJsonConverter(true)
            },
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
        public static readonly JsonSerializerOptions SwarmJsonSerializerOptions = new()
        {
            Converters =
            {
                new BzzValueJsonConverter(true),
                new DateTimeOffsetAsUnixSecondsJsonConverter(),
                new EncryptionKey256JsonConverter(),
                new EthAddressJsonConverter(),
                new EthTxHashJsonConverter(),
                new JsonStringEnumConverter(),
                new PostageBatchIdJsonConverter(),
                new PostageStampJsonConverter(),
                new SwarmAddressJsonConverter(),
                new SwarmFeedTopicJsonConverter(),
                new SwarmHashJsonConverter(),
                new SwarmOverlayAddressJsonConverter(),
                new SwarmReferenceJsonConverter(),
                new SwarmSocIdentifierJsonConverter(),
                new SwarmSocSignatureJsonConverter(),
                new SwarmUriJsonConverter(),
                new TagIdJsonConverter(),
                new TimeSpanAsSecondsJsonConverter(),
                new XDaiValueJsonConverter(true)
            },
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }
}
