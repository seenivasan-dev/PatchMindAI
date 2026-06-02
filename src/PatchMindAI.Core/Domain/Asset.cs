using PatchMindAI.Core.Enums;

namespace PatchMindAI.Core.Domain;

/// <summary>
/// Represents an organizational asset/system that may be affected by CVEs
/// </summary>
public sealed class Asset
{
    public Guid Id { get; set; }

    public string Hostname { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public string OperatingSystem { get; set; } = string.Empty;

    public AssetType Type { get; set; } = AssetType.Server;

    public AssetCriticality Criticality { get; set; } = AssetCriticality.Medium;

    public string Environment { get; set; } = string.Empty; // e.g., Production, Staging, Development

    public string Location { get; set; } = string.Empty; // e.g., US-East, EU-West

    public bool IsInternetFacing { get; set; }

    public string[] InstalledSoftware { get; set; } = Array.Empty<string>();

    public string Owner { get; set; } = string.Empty;

    public string BusinessUnit { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime LastScannedAtUtc { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation property
    public ICollection<PatchStatus> PatchStatuses { get; set; } = new List<PatchStatus>();
}
