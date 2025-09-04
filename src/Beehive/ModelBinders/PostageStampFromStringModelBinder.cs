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
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Threading.Tasks;

namespace Etherna.Beehive.ModelBinders
{
    public class PostageStampFromStringModelBinder : IModelBinder
    {
        public Task BindModelAsync(ModelBindingContext bindingContext)
        {
            ArgumentNullException.ThrowIfNull(bindingContext, nameof(bindingContext));

            var valueProviderResult = bindingContext.ValueProvider.GetValue(bindingContext.ModelName);
            if (valueProviderResult == ValueProviderResult.None)
                return Task.CompletedTask;

            bindingContext.ModelState.SetModelValue(bindingContext.ModelName, valueProviderResult);

            // Try to convert the value.
            var value = valueProviderResult.FirstValue;
            if (value != null && value.IsHex() && value.Length == PostageStamp.StampSize * 2)
            {
                var postageStamp = PostageStamp.FromByteArray(value.HexToByteArray());
                bindingContext.Result = ModelBindingResult.Success(postageStamp);
                return Task.CompletedTask;
            }

            // In case of error.
            bindingContext.ModelState.TryAddModelError(bindingContext.ModelName, $"The value '{value}' is not valid.");
            return Task.CompletedTask;
        }
    }
}