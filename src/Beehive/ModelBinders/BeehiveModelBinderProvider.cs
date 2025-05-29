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

using Etherna.Beehive.Domain.Models;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using System;
using System.IO;

namespace Etherna.Beehive.ModelBinders
{
    public class BeehiveModelBinderProvider : IModelBinderProvider
    {
        public IModelBinder? GetBinder(ModelBinderProviderContext context)
        {
            ArgumentNullException.ThrowIfNull(context, nameof(context));

            if (context.Metadata.ModelType == typeof(DateTimeOffset) || 
                context.Metadata.ModelType == typeof(DateTimeOffset?))
                return new DateTimeOffsetFromUnixTimeSecondsModelBinder();

            if (context.Metadata.ModelType == typeof(PostageStamp))
                return new PostageStampFromStringModelBinder();

            if (context.Metadata.ModelType == typeof(TimeSpan) ||
                context.Metadata.ModelType == typeof(TimeSpan?))
                return new TimeSpanFromSecondsModelBinder();

            if (context.Metadata.ModelType == typeof(Stream))
                return new StreamFromHttpRequestBodyModelBinder();

            return null;
        }
    }
}