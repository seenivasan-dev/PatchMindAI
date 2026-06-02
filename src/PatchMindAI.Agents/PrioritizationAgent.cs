using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Core.Models;
using PatchMindAI.Infrastructure.Data;

namespace PatchMindAI.Agents;

/// <summary>
/// Calculates vulnerability priority scores based on CVSS, asset criticality, and exposure.
/// </summary>
public sealed class PrioritizationAgent : IPrioritizationAgent
{
    private readonly PatchMindDbContext _context;
    private readonly ILogger<PrioritizationAgent> _logger;

    public PrioritizationAgent(
        PatchMindDbContext context,
        ILogger<PrioritizationAgent> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<PrioritizedVulnerability>> GetPrioritizedVulnerabilitiesAsync(
        int topN = 20,
        CancellationToken cancellationToken = default)
    {
        // Query all vulnerable patch statuses with related CVE and Asset data
        var vulnerablePatchStatuses = await _context.PatchStatuses
            .Include(ps => ps.Cve)
            .Include(ps => ps.Asset)
            .Where(ps => ps.Status == PatchingStatus.Vulnerable)
            .ToListAsync(cancellationToken);

        _logger.LogInformation("Found {Count} vulnerable patch statuses", vulnerablePatchStatuses.Count);

        var prioritizedList = new List<PrioritizedVulnerability>();

        foreach (var patchStatus in vulnerablePatchStatuses)
        {
            if (patchStatus.Cve is null || patchStatus.Asset is null)
            {
                _logger.LogWarning("PatchStatus {Id} missing CVE or Asset data", patchStatus.Id);
                continue;
            }

            var computedScore = CalculateRiskScore(
                patchStatus.Cve,
                patchStatus.Asset,
                patchStatus.DetectedAtUtc);

            var details = BuildRiskCalculationDetails(
                patchStatus.Cve,
                patchStatus.Asset,
                patchStatus.DetectedAtUtc);

            prioritizedList.Add(new PrioritizedVulnerability
            {
                Cve = patchStatus.Cve,
                Asset = patchStatus.Asset,
                PatchStatus = patchStatus,
                ComputedRiskScore = computedScore,
                RiskCalculationDetails = details,
                DaysOpen = (int)(DateTime.UtcNow - patchStatus.DetectedAtUtc).TotalDays,
                IsInternetFacing = patchStatus.Asset.IsInternetFacing
            });
        }

        // Sort by computed risk score (highest first), then by days open
        var sorted = prioritizedList
            .OrderByDescending(v => v.ComputedRiskScore)
            .ThenByDescending(v => v.DaysOpen)
            .Take(topN)
            .ToList();

        _logger.LogInformation("Prioritized {Count} vulnerabilities", sorted.Count);
        return sorted;
    }

    public async Task<double> CalculateRiskScoreAsync(
        string cveId,
        Guid assetId,
        CancellationToken cancellationToken = default)
    {
        var patchStatus = await _context.PatchStatuses
            .Include(ps => ps.Cve)
            .Include(ps => ps.Asset)
            .FirstOrDefaultAsync(ps => ps.CveId == cveId && ps.AssetId == assetId, cancellationToken);

        if (patchStatus?.Cve is null || patchStatus.Asset is null)
        {
            _logger.LogWarning("PatchStatus not found for CVE {CveId} and Asset {AssetId}", cveId, assetId);
            return 0.0;
        }

        return CalculateRiskScore(patchStatus.Cve, patchStatus.Asset, patchStatus.DetectedAtUtc);
    }

    /// <summary>
    /// Calculates risk score (0-100) using weighted formula:
    /// Score = (CVSS × 0.5) + (AssetCriticality × 0.3) + (ExposureBonus × 0.15) + (AgeMultiplier × 0.05)
    /// </summary>
    private double CalculateRiskScore(
        Core.Domain.Cve cve,
        Core.Domain.Asset asset,
        DateTime detectedAtUtc)
    {
        // CVSS component (0-10 normalized to 0-50)
        var cvssComponent = (cve.BaseScore / 10.0) * 50.0;

        // Asset criticality component (0-30)
        var criticalityComponent = asset.Criticality switch
        {
            AssetCriticality.Critical => 30.0,
            AssetCriticality.High => 22.5,
            AssetCriticality.Medium => 15.0,
            AssetCriticality.Low => 7.5,
            _ => 0.0
        };

        // Internet-facing exposure bonus (0-15)
        var exposureComponent = asset.IsInternetFacing ? 15.0 : 0.0;

        // Age multiplier (0-5) - vulnerabilities open longer get higher priority
        var daysOpen = (DateTime.UtcNow - detectedAtUtc).TotalDays;
        var ageComponent = daysOpen switch
        {
            > 90 => 5.0,   // Over 3 months
            > 60 => 4.0,   // Over 2 months
            > 30 => 3.0,   // Over 1 month
            > 14 => 2.0,   // Over 2 weeks
            > 7 => 1.0,    // Over 1 week
            _ => 0.5
        };

        var totalScore = cvssComponent + criticalityComponent + exposureComponent + ageComponent;
        
        // Ensure score is within 0-100 range
        return Math.Clamp(totalScore, 0.0, 100.0);
    }

    private string BuildRiskCalculationDetails(
        Core.Domain.Cve cve,
        Core.Domain.Asset asset,
        DateTime detectedAtUtc)
    {
        var daysOpen = (int)(DateTime.UtcNow - detectedAtUtc).TotalDays;
        
        var details = $"CVSS: {cve.BaseScore:F1} ({cve.Severity}), " +
                     $"Asset Criticality: {asset.Criticality}, " +
                     $"Internet-Facing: {(asset.IsInternetFacing ? "Yes" : "No")}, " +
                     $"Days Open: {daysOpen}";

        return details;
    }
}
