namespace PatchMindAI.Core.Models;

/// <summary>
/// Represents a weekly vulnerability management report.
/// </summary>
public sealed class WeeklyReport
{
    public DateTime ReportStartDate { get; init; }
    public DateTime ReportEndDate { get; init; }
    public DateTime GeneratedAtUtc { get; init; }
    
    public WeeklyStatistics Statistics { get; init; } = null!;
    public List<TrendItem> Trends { get; init; } = [];
    public List<PrioritizedVulnerability> TopVulnerabilities { get; init; } = [];
    public List<string> KeyFindings { get; init; } = [];
    public string ExecutiveSummary { get; init; } = string.Empty;
}

public sealed class WeeklyStatistics
{
    public int TotalVulnerabilities { get; init; }
    public int CriticalVulnerabilities { get; init; }
    public int HighVulnerabilities { get; init; }
    public int PatchedThisWeek { get; init; }
    public int NewlyDiscovered { get; init; }
    public int OverduePatches { get; init; }
    public double AveragePatchTime { get; init; }
    public int TotalAffectedAssets { get; init; }
    public int InternetFacingAffected { get; init; }
}

public sealed class TrendItem
{
    public string Category { get; init; } = string.Empty;
    public string Direction { get; init; } = string.Empty; // "increasing", "decreasing", "stable"
    public double ChangePercentage { get; init; }
    public string Description { get; init; } = string.Empty;
}
