using System.ComponentModel.DataAnnotations;

namespace Etherna.BeehiveManager.Areas.Api.InputModels
{
    public class BeeNodeInput
    {
        [Range(1, 65535)]
        public int? DebugApiPort { get; set; }

        [Range(1, 65535)]
        public int? GatwayApiPort { get; set; }

        [Required]
        public string Url { get; set; } = default!;
    }
}
