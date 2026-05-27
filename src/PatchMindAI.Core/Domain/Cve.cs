using PatchMindAI.Core.Enums;

namespace PatchMindAI.Core.Domain;

public sealed class Cve
{
    public string Id { get; set; } = string.Empty;

    public DateTime PublishedAtUtc { get; set; }

    public DateTime LastModifiedAtUtc { get; set; }

    public string Description { get; set; } = string.Empty;

    public double BaseScore { get; set; }

    public SeverityLevel Severity { get; set; } = SeverityLevel.None;

    public string VectorString { get; set; } = string.Empty;

    public string[] Weaknesses { get; set; } = Array.Empty<string>();

    public string[] AffectedProducts { get; set; } = Array.Empty<string>();

    public string[] References { get; set; } = Array.Empty<string>();

    public DateTime SyncedAtUtc { get; set; }
}
