namespace PatchMindAI.Core.Domain;

/// <summary>
/// Audit log entry stored in database for compliance tracking.
/// </summary>
public sealed class AuditLog
{
    public Guid Id { get; init; }
    public DateTime TimestampUtc { get; init; }
    public string EventType { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string? UserQuery { get; init; }
    public string? JobId { get; init; }
    public string? CveId { get; init; }
    public string Action { get; init; } = string.Empty;
    public string? Details { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
}
