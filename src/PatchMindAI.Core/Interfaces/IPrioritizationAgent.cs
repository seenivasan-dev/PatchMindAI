using PatchMindAI.Core.Models;

namespace PatchMindAI.Core.Interfaces;

/// <summary>
/// Calculates priority scores for vulnerabilities based on CVSS, asset criticality, and exposure.
/// </summary>
public interface IPrioritizationAgent
{
    /// <summary>
    /// Generates a prioritized list of vulnerabilities for remediation.
    /// </summary>
    Task<List<PrioritizedVulnerability>> GetPrioritizedVulnerabilitiesAsync(
        int topN = 20,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Calculates a risk score for a specific CVE-Asset combination.
    /// </summary>
    Task<double> CalculateRiskScoreAsync(
        string cveId,
        Guid assetId,
        CancellationToken cancellationToken = default);
}
