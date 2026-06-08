using PatchMindAI.Agents;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Models;

namespace PatchMindAI.Tests.Unit.Services;

public class DeterministicRiskScorerTests
{
    [Fact]
    public void Score_ShouldBeDeterministic_ForSameInputs()
    {
        var scorer = new DeterministicRiskScorer();
        var cve = new Cve
        {
            Id = "CVE-2021-44228",
            BaseScore = 9.8,
            Severity = SeverityLevel.Critical
        };

        var facts = new SqlFactSnapshot
        {
            CveId = cve.Id,
            TotalVulnerableAssets = 12,
            InternetFacingVulnerableAssets = 4,
            OverduePatches = 3,
            CriticalAssetsAffected = 2,
            HighAssetsAffected = 3,
            AverageDaysOpen = 45
        };

        var first = scorer.Score(cve, facts);
        var second = scorer.Score(cve, facts);

        Assert.Equal(first.OverallScore, second.OverallScore);
        Assert.Equal(first.CvssComponent, second.CvssComponent);
        Assert.Equal(first.CriticalityComponent, second.CriticalityComponent);
        Assert.Equal(first.ExposureComponent, second.ExposureComponent);
        Assert.Equal(first.OverdueComponent, second.OverdueComponent);
        Assert.Equal(first.AgeComponent, second.AgeComponent);
    }
}
