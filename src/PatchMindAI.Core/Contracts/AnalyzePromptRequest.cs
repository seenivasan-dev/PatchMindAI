using System.ComponentModel.DataAnnotations;

namespace PatchMindAI.Core.Contracts;

public sealed class AnalyzePromptRequest
{
    [Required]
    [MaxLength(2000)]
    public string Question { get; set; } = string.Empty;
}