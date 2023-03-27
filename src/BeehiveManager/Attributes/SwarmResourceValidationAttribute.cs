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
