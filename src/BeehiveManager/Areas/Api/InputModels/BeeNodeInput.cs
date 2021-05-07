using System.ComponentModel.DataAnnotations;

namespace Etherna.BeehiveManager.Areas.Api.InputModels
{
    public class BeeNodeInput
    {
        public int? DebugApiPort { get; set; }
        public int? GatwayApiPort { get; set; }
        [Required]
        public string Url { get; set; } = default!;
    }
}
