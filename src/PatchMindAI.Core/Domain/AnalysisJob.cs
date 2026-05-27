using PatchMindAI.Core.Enums;

namespace PatchMindAI.Core.Domain;

public sealed class AnalysisJob
{
    public Guid Id { get; set; }

    public string CveId { get; set; } = string.Empty;

    public string UserQuery { get; set; } = string.Empty;

    public JobStatus Status { get; set; } = JobStatus.Queued;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string? FailureReason { get; set; }
}
