namespace PatchMindAI.Core.Domain;

public sealed class AnalysisResult
{
    public Guid Id { get; set; }

    public Guid JobId { get; set; }

    public double RiskScore { get; set; }

    public string RiskJustification { get; set; } = string.Empty;

    public string ImpactSummary { get; set; } = string.Empty;

    public string AffectedAssetsJson { get; set; } = "[]";

    public string RemediationStepsJson { get; set; } = "[]";

    public string RawAgentOutputJson { get; set; } = "{}";

    public DateTime GeneratedAtUtc { get; set; }
}
