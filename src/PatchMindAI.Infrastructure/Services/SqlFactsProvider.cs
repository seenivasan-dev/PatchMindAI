using Microsoft.EntityFrameworkCore;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Core.Models;
using PatchMindAI.Infrastructure.Data;

namespace PatchMindAI.Infrastructure.Services;

public sealed class SqlFactsProvider : ISqlFactsProvider
{
    private readonly PatchMindDbContext _dbContext;

    public SqlFactsProvider(PatchMindDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SqlFactSnapshot> GetFactsForCveAsync(string cveId, int topAssets = 10, CancellationToken cancellationToken = default)
    {
        var patchRows = await _dbContext.PatchStatuses
            .Include(ps => ps.Asset)
            .Where(ps => ps.CveId == cveId && ps.Status == PatchingStatus.Vulnerable)
            .ToListAsync(cancellationToken);

        if (patchRows.Count == 0)
        {
            return new SqlFactSnapshot
            {
                CveId = cveId,
                RankedAssets = Array.Empty<RankedAssetExposure>()
            };
        }

        var now = DateTime.UtcNow;
        var ranked = patchRows
            .Where(ps => ps.Asset is not null)
            .Select(ps =>
            {
                var asset = ps.Asset!;
                var daysOpen = Math.Max(0, (int)(now - ps.DetectedAtUtc).TotalDays);
                var criticalityScore = asset.Criticality switch
                {
                    AssetCriticality.Critical => 70,
                    AssetCriticality.High => 50,
                    AssetCriticality.Medium => 30,
                    AssetCriticality.Low => 15,
                    _ => 0
                };
                var exposureScore = asset.IsInternetFacing ? 20 : 0;
                var ageScore = Math.Min(10, daysOpen / 14.0 * 10.0);
                var score = Math.Round(Math.Clamp(criticalityScore + exposureScore + ageScore, 0, 100), 2);

                return new RankedAssetExposure
                {
                    AssetId = asset.Id,
                    Hostname = asset.Hostname,
                    Criticality = asset.Criticality,
                    IsInternetFacing = asset.IsInternetFacing,
                    DaysOpen = daysOpen,
                    PriorityScore = score
                };
            })
            .OrderByDescending(a => a.PriorityScore)
            .ThenByDescending(a => a.DaysOpen)
            .Take(Math.Clamp(topAssets, 1, 50))
            .ToArray();

        var withAsset = patchRows.Where(ps => ps.Asset is not null).Select(ps => ps.Asset!).ToArray();

        return new SqlFactSnapshot
        {
            CveId = cveId,
            TotalVulnerableAssets = withAsset.Length,
            InternetFacingVulnerableAssets = withAsset.Count(a => a.IsInternetFacing),
            OverduePatches = patchRows.Count(ps => ps.TargetPatchDate.HasValue && ps.TargetPatchDate.Value < now),
            CriticalAssetsAffected = withAsset.Count(a => a.Criticality == AssetCriticality.Critical),
            HighAssetsAffected = withAsset.Count(a => a.Criticality == AssetCriticality.High),
            AverageDaysOpen = patchRows.Average(ps => Math.Max(0, (now - ps.DetectedAtUtc).TotalDays)),
            RankedAssets = ranked
        };
    }
}
