using System.Text.Json;
using Moq;
using PatchMindAI.Agents;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Core.Models;

namespace PatchMindAI.Tests.Unit.Services;

public class MockAnalysisOrchestratorTests
{
    [Fact]
    public async Task RunAsync_ShouldIncludeSqlFactsAndDeterministicScoreInRawOutput()
    {
        var nvdClient = new Mock<INvdClient>();
        var knowledgeRetriever = new Mock<IKnowledgeRetriever>();
        var sqlFactsProvider = new Mock<ISqlFactsProvider>();
        var scorer = new Mock<IDeterministicRiskScorer>();

        var cve = new Cve
        {
            Id = "CVE-2021-44228",
            BaseScore = 9.8,
            Severity = SeverityLevel.Critical,
            Description = "Log4Shell"
        };

        nvdClient.Setup(x => x.GetCveByIdAsync(cve.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(cve);

        knowledgeRetriever.Setup(x => x.RetrieveAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new RetrievedChunk
                {
                    SourceId = cve.Id,
                    Text = "retrieved context",
                    Score = 0.91
                }
            });

        var facts = new SqlFactSnapshot
        {
            CveId = cve.Id,
            TotalVulnerableAssets = 3,
            InternetFacingVulnerableAssets = 1,
            RankedAssets = new[]
            {
                new RankedAssetExposure
                {
                    AssetId = Guid.NewGuid(),
                    Hostname = "prod-web-01",
                    Criticality = AssetCriticality.Critical,
                    IsInternetFacing = true,
                    DaysOpen = 25,
                    PriorityScore = 95
                }
            }
        };

        var score = new RiskScoringResult
        {
            OverallScore = 87.4,
            Justification = "Deterministic SQL grounded score"
        };

        sqlFactsProvider.Setup(x => x.GetFactsForCveAsync(cve.Id, 10, It.IsAny<CancellationToken>()))
            .ReturnsAsync(facts);

        scorer.Setup(x => x.Score(cve, facts)).Returns(score);

        var orchestrator = new MockAnalysisOrchestrator(
            nvdClient.Object,
            knowledgeRetriever.Object,
            sqlFactsProvider.Object,
            scorer.Object);

        var result = await orchestrator.RunAsync(new AnalysisJob
        {
            Id = Guid.NewGuid(),
            CveId = cve.Id,
            UserQuery = "analyze",
            Status = JobStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow
        });

        Assert.Equal(score.OverallScore, result.RiskScore);

        using var json = JsonDocument.Parse(result.RawAgentOutputJson);
        Assert.True(json.RootElement.TryGetProperty("sqlFacts", out var sqlFactsNode));
        var hasTotal = sqlFactsNode.TryGetProperty("totalVulnerableAssets", out var totalNode)
            || sqlFactsNode.TryGetProperty("TotalVulnerableAssets", out totalNode);
        Assert.True(hasTotal);
        Assert.Equal(3, totalNode.GetInt32());
        Assert.True(json.RootElement.TryGetProperty("deterministicScore", out _));
    }
}
