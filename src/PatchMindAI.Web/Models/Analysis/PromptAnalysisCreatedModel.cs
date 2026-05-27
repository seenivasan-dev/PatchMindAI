namespace PatchMindAI.Web.Models.Analysis;

public sealed class PromptAnalysisCreatedModel
{
    public Guid JobId { get; set; }

    public string Status { get; set; } = string.Empty;

    public string? MatchedCveId { get; set; }

    public bool IsExactMatch { get; set; }

    public double Confidence { get; set; }

    public string Explanation { get; set; } = string.Empty;
}