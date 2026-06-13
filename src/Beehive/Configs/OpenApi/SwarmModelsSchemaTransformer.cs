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
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Etherna.Beehive.Configs.OpenApi
{
    public sealed class SwarmModelsSchemaTransformer : IOpenApiSchemaTransformer
    {
        public Task TransformAsync(
            OpenApiSchema schema,
            OpenApiSchemaTransformerContext context,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(schema);
            ArgumentNullException.ThrowIfNull(context);

            /* Enums are serialized as strings by the global JsonStringEnumConverter, but the
             * JsonSchemaExporter emits their schema with the enum values and no "type". Restore it,
             * and document that the binding also accepts the integer value (see
             * CaseInsensitiveEnumBindingMiddleware). */
            var underlyingType = Nullable.GetUnderlyingType(context.JsonTypeInfo.Type) ?? context.JsonTypeInfo.Type;
            if (underlyingType.IsEnum && schema.Enum is { Count: > 0 })
            {
                schema.Type = JsonSchemaType.String;
                schema.Description = "Accepts the value name (case-insensitive) or its integer equivalent: " +
                                     $"{EnumApiConventions.FormatAllowedValues(underlyingType)}.";
            }

            if (context.JsonTypeInfo.Type == typeof(BzzValue) || context.JsonTypeInfo.Type == typeof(BzzValue?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
            }
            if (context.JsonTypeInfo.Type == typeof(DateTimeOffset) || context.JsonTypeInfo.Type == typeof(DateTimeOffset?))
            {
                schema.Type = JsonSchemaType.Integer;
                schema.Format = "int64";
            }
            if (context.JsonTypeInfo.Type == typeof(EthAddress) || context.JsonTypeInfo.Type == typeof(EthAddress?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
                schema.MinLength = EthAddress.AddressSize * 2;
                schema.MaxLength = EthAddress.AddressSize * 2 + 2;
                schema.Pattern = $"^(0x)?[a-fA-F0-9]{{{EthAddress.AddressSize * 2}}}$";
            }
            if (context.JsonTypeInfo.Type == typeof(EthTxHash) || context.JsonTypeInfo.Type == typeof(EthTxHash?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
                schema.MinLength = EthTxHash.HashSize * 2;
                schema.MaxLength = EthTxHash.HashSize * 2;
                schema.Pattern = $"^[a-fA-F0-9]{{{EthTxHash.HashSize * 2}}}$";
            }
            if (context.JsonTypeInfo.Type == typeof(PostageBatchId) || context.JsonTypeInfo.Type == typeof(PostageBatchId?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
                schema.MinLength = PostageBatchId.BatchIdSize * 2;
                schema.MaxLength = PostageBatchId.BatchIdSize * 2;
                schema.Pattern = $"^[a-fA-F0-9]{{{PostageBatchId.BatchIdSize * 2}}}$";
            }
            if (context.JsonTypeInfo.Type == typeof(PostageStamp) || context.JsonTypeInfo.Type == typeof(PostageStamp?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
                schema.MinLength = PostageStamp.StampSize * 2;
                schema.MaxLength = PostageStamp.StampSize * 2;
                schema.Pattern = $"^[a-fA-F0-9]{{{PostageStamp.StampSize * 2}}}$";
                schema.Properties?.Clear();
            }
            if (context.JsonTypeInfo.Type == typeof(SwarmAddress) || context.JsonTypeInfo.Type == typeof(SwarmAddress?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
                schema.MinLength = SwarmReference.PlainSize * 2;
                schema.Pattern = $"^[a-fA-F0-9]{{{SwarmReference.PlainSize * 2}}}.*$";
                schema.Properties?.Clear();
            }
            if (context.JsonTypeInfo.Type == typeof(SwarmFeedTopic) || context.JsonTypeInfo.Type == typeof(SwarmFeedTopic?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
                schema.MinLength = SwarmFeedTopic.TopicSize * 2;
                schema.MaxLength = SwarmFeedTopic.TopicSize * 2;
                schema.Pattern = $"^[a-fA-F0-9]{{{SwarmFeedTopic.TopicSize * 2}}}$";
            }
            if (context.JsonTypeInfo.Type == typeof(SwarmHash) || context.JsonTypeInfo.Type == typeof(SwarmHash?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
                schema.MinLength = SwarmHash.HashSize * 2;
                schema.MaxLength = SwarmHash.HashSize * 2;
                schema.Pattern = $"^[a-fA-F0-9]{{{SwarmHash.HashSize * 2}}}$";
            }
            if (context.JsonTypeInfo.Type == typeof(SwarmOverlayAddress) || context.JsonTypeInfo.Type == typeof(SwarmOverlayAddress?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
                schema.MinLength = SwarmOverlayAddress.AddressSize * 2;
                schema.MaxLength = SwarmOverlayAddress.AddressSize * 2;
                schema.Pattern = $"^[a-fA-F0-9]{{{SwarmOverlayAddress.AddressSize * 2}}}$";
            }

            if (context.JsonTypeInfo.Type == typeof(SwarmReference) || context.JsonTypeInfo.Type == typeof(SwarmReference?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
                schema.MinLength = SwarmReference.PlainSize * 2;
                schema.MaxLength = SwarmReference.EncryptedSize * 2;
                schema.Pattern = $"^([a-fA-F0-9]{{{SwarmReference.PlainSize * 2}}}|[a-fA-F0-9]{{{SwarmReference.EncryptedSize * 2}}})$";
                schema.Properties?.Clear();
            }
            if (context.JsonTypeInfo.Type == typeof(SwarmSocIdentifier) || context.JsonTypeInfo.Type == typeof(SwarmSocIdentifier?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
                schema.MinLength = SwarmSocIdentifier.IdentifierSize * 2;
                schema.MaxLength = SwarmSocIdentifier.IdentifierSize * 2;
                schema.Pattern = $"^[a-fA-F0-9]{{{SwarmSocIdentifier.IdentifierSize * 2}}}$";
            }
            if (context.JsonTypeInfo.Type == typeof(SwarmSocSignature) || context.JsonTypeInfo.Type == typeof(SwarmSocSignature?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
                schema.MinLength = SwarmSocSignature.SignatureSize * 2;
                schema.MaxLength = SwarmSocSignature.SignatureSize * 2;
                schema.Pattern = $"^[a-fA-F0-9]{{{SwarmSocSignature.SignatureSize * 2}}}$";
            }
            if (context.JsonTypeInfo.Type == typeof(SwarmUri) || context.JsonTypeInfo.Type == typeof(SwarmUri?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
            }
            if (context.JsonTypeInfo.Type == typeof(TagId) || context.JsonTypeInfo.Type == typeof(TagId?))
            {
                schema.Type = JsonSchemaType.Integer;
                schema.Format = "int64";
            }
            if (context.JsonTypeInfo.Type == typeof(TimeSpan) || context.JsonTypeInfo.Type == typeof(TimeSpan?))
            {
                schema.Type = JsonSchemaType.Integer;
                schema.Format = "int64";
                schema.Pattern = null;
            }
            if (context.JsonTypeInfo.Type == typeof(XDaiValue) || context.JsonTypeInfo.Type == typeof(XDaiValue?))
            {
                schema.Type = JsonSchemaType.String;
                schema.Format = null;
            }

            return Task.CompletedTask;
        }
    }
}