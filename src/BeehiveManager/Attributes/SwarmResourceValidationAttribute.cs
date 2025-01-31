﻿// Copyright 2021-present Etherna SA
// This file is part of BeehiveManager.
// 
// BeehiveManager is free software: you can redistribute it and/or modify it under the terms of the
// GNU Affero General Public License as published by the Free Software Foundation,
// either version 3 of the License, or (at your option) any later version.
// 
// BeehiveManager is distributed in the hope that it will be useful, but WITHOUT ANY WARRANTY;
// without even the implied warranty of MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
// See the GNU Affero General Public License for more details.
// 
// You should have received a copy of the GNU Affero General Public License along with BeehiveManager.
// If not, see <https://www.gnu.org/licenses/>.

using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;

namespace Etherna.BeehiveManager.Attributes
{
    public sealed partial class SwarmResourceValidationAttribute : ValidationAttribute
    {
        [GeneratedRegex("^[A-Fa-f0-9]{64}$")]
        private static partial Regex SwarmResourceRegex();

        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            if (value is string stringValue && SwarmResourceRegex().IsMatch(stringValue))
                return ValidationResult.Success;

            return new ValidationResult("Argument is not a valid swarm resource");
        }
    }
}
