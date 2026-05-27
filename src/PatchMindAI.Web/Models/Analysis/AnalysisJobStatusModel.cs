namespace PatchMindAI.Web.Models.Analysis;

public sealed class AnalysisJobStatusModel
{
    public Guid JobId { get; set; }

    public string CveId { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string? FailureReason { get; set; }
}
