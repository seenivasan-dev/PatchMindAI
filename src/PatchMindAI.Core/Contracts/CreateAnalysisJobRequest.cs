using System.ComponentModel.DataAnnotations;

namespace PatchMindAI.Core.Contracts;

public sealed class CreateAnalysisJobRequest
{
    [Required]
    [RegularExpression("^CVE-\\d{4}-\\d{4,}$", ErrorMessage = "cveId must match CVE-YYYY-NNNN format.")]
    public string CveId { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string UserQuery { get; set; } = string.Empty;
}
