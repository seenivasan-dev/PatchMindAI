using PatchMindAI.Core.Enums;

namespace PatchMindAI.Core.Models;

/// <summary>
/// Represents the parsed intent from a user query.
/// </summary>
public sealed class ParsedIntent
{
    public QueryIntent Intent { get; init; }
    public string OriginalQuery { get; init; } = string.Empty;
    public string? ExtractedCveId { get; init; }
    public string? ExtractedKeywords { get; init; }
    public int? TopN { get; init; }
    public TimeRange? TimeRange { get; init; }
    public double Confidence { get; init; }
}

public sealed class TimeRange
{
    public DateTime? StartDate { get; init; }
    public DateTime? EndDate { get; init; }
    public string? Description { get; init; }
}
