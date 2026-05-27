namespace PatchMindAI.Core.Contracts;

public sealed class CvePromptResolution
{
    public bool IsResolved { get; set; }

    public bool IsExactMatch { get; set; }

    public string? MatchedCveId { get; set; }

    public double Confidence { get; set; }

    public string Explanation { get; set; } = string.Empty;

    public IReadOnlyList<string> CandidateCveIds { get; set; } = Array.Empty<string>();
}