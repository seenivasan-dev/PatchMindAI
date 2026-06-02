using PatchMindAI.Core.Enums;

namespace PatchMindAI.Core.Domain;

/// <summary>
/// Tracks the patching status of a specific CVE for a specific asset
/// </summary>
public sealed class PatchStatus
{
    public Guid Id { get; set; }

    public string CveId { get; set; } = string.Empty;

    public Guid AssetId { get; set; }

    public PatchingStatus Status { get; set; } = PatchingStatus.Vulnerable;

    public DateTime DetectedAtUtc { get; set; }

    public DateTime? PatchedAtUtc { get; set; }

    public string? PatchVersion { get; set; }

    public string? Notes { get; set; }

    public PatchPriority Priority { get; set; } = PatchPriority.Medium;

    public DateTime? TargetPatchDate { get; set; }

    public string? AssignedTo { get; set; }

    // Navigation properties
    public Cve? Cve { get; set; }

    public Asset? Asset { get; set; }
}
