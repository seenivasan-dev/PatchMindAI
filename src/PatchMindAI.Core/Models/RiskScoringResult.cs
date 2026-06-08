namespace PatchMindAI.Core.Models;

public sealed class RiskScoringResult
{
    public double OverallScore { get; init; }

    public string Justification { get; init; } = string.Empty;

    public double CvssComponent { get; init; }

    public double CriticalityComponent { get; init; }

    public double ExposureComponent { get; init; }

    public double OverdueComponent { get; init; }

    public double AgeComponent { get; init; }
}
