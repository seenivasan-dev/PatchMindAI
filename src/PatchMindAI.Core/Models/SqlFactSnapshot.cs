using PatchMindAI.Core.Enums;

namespace PatchMindAI.Core.Models;

public sealed class SqlFactSnapshot
{
    public string CveId { get; init; } = string.Empty;

    public int TotalVulnerableAssets { get; init; }

    public int InternetFacingVulnerableAssets { get; init; }

    public int OverduePatches { get; init; }

    public int CriticalAssetsAffected { get; init; }

    public int HighAssetsAffected { get; init; }

    public double AverageDaysOpen { get; init; }

    public IReadOnlyList<RankedAssetExposure> RankedAssets { get; init; } = Array.Empty<RankedAssetExposure>();
}

public sealed class RankedAssetExposure
{
    public Guid AssetId { get; init; }

    public string Hostname { get; init; } = string.Empty;

    public AssetCriticality Criticality { get; init; }

    public bool IsInternetFacing { get; init; }

    public int DaysOpen { get; init; }

    public double PriorityScore { get; init; }
}
