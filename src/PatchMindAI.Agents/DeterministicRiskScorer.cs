using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Core.Models;

namespace PatchMindAI.Agents;

public sealed class DeterministicRiskScorer : IDeterministicRiskScorer
{
    public RiskScoringResult Score(Cve cve, SqlFactSnapshot facts)
    {
        var cvssComponent = Math.Clamp((cve.BaseScore / 10.0) * 55.0, 0, 55);
        var criticalityComponent = Math.Min(20.0, (facts.CriticalAssetsAffected * 4.0) + (facts.HighAssetsAffected * 2.0));
        var exposureComponent = Math.Min(15.0, facts.InternetFacingVulnerableAssets * 2.5);
        var overdueComponent = Math.Min(7.0, facts.OverduePatches * 1.75);
        var ageComponent = Math.Min(3.0, (facts.AverageDaysOpen / 30.0) * 3.0);

        var overall = Math.Round(Math.Clamp(
            cvssComponent + criticalityComponent + exposureComponent + overdueComponent + ageComponent,
            0,
            100), 2);

        var justification =
            $"Deterministic score combines CVSS {cve.BaseScore:F1} with SQL facts: " +
            $"{facts.TotalVulnerableAssets} vulnerable assets, " +
            $"{facts.InternetFacingVulnerableAssets} internet-facing, " +
            $"{facts.OverduePatches} overdue patches, " +
            $"{facts.CriticalAssetsAffected} critical assets affected.";

        return new RiskScoringResult
        {
            OverallScore = overall,
            Justification = justification,
            CvssComponent = Math.Round(cvssComponent, 2),
            CriticalityComponent = Math.Round(criticalityComponent, 2),
            ExposureComponent = Math.Round(exposureComponent, 2),
            OverdueComponent = Math.Round(overdueComponent, 2),
            AgeComponent = Math.Round(ageComponent, 2)
        };
    }
}
