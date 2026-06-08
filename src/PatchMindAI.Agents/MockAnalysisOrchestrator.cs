using System.Text.Json;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Agents;

public sealed class MockAnalysisOrchestrator : IAnalysisOrchestrator
{
    private readonly INvdClient _nvdClient;
    private readonly IKnowledgeRetriever _knowledgeRetriever;
    private readonly ISqlFactsProvider _sqlFactsProvider;
    private readonly IDeterministicRiskScorer _riskScorer;

    public MockAnalysisOrchestrator(
        INvdClient nvdClient,
        IKnowledgeRetriever knowledgeRetriever,
        ISqlFactsProvider sqlFactsProvider,
        IDeterministicRiskScorer riskScorer)
    {
        _nvdClient = nvdClient;
        _knowledgeRetriever = knowledgeRetriever;
        _sqlFactsProvider = sqlFactsProvider;
        _riskScorer = riskScorer;
    }

    public async Task<AnalysisResult> RunAsync(AnalysisJob job, CancellationToken cancellationToken = default)
    {
        var cve = await _nvdClient.GetCveByIdAsync(job.CveId, cancellationToken);
        if (cve is null)
        {
            throw new InvalidOperationException($"CVE '{job.CveId}' was not found.");
        }

        await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);

        var retrievalQuery = string.IsNullOrWhiteSpace(job.UserQuery) ? job.CveId : job.UserQuery;
        var retrievedChunks = await _knowledgeRetriever.RetrieveAsync(retrievalQuery, 5, cancellationToken);
        if (retrievedChunks.Count == 0 && !retrievalQuery.Equals(job.CveId, StringComparison.OrdinalIgnoreCase))
        {
            retrievedChunks = await _knowledgeRetriever.RetrieveAsync(job.CveId, 5, cancellationToken);
        }

        var sqlFacts = await _sqlFactsProvider.GetFactsForCveAsync(job.CveId, 10, cancellationToken);
        var scoring = _riskScorer.Score(cve, sqlFacts);

        var remediation = new[]
        {
            new { priority = "Critical", action = $"Patch affected products listed for {cve.Id}." },
            new { priority = "High", action = "Apply temporary mitigations and monitor exploit attempts." }
        };

        return new AnalysisResult
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            RiskScore = scoring.OverallScore,
            RiskJustification = scoring.Justification,
            ImpactSummary = $"{cve.Id} currently affects {sqlFacts.TotalVulnerableAssets} assets, including {sqlFacts.InternetFacingVulnerableAssets} internet-facing systems.",
            AffectedAssetsJson = JsonSerializer.Serialize(sqlFacts.RankedAssets),
            RemediationStepsJson = JsonSerializer.Serialize(remediation),
            RawAgentOutputJson = JsonSerializer.Serialize(new
            {
                planner = "mock",
                reflectionApplied = true,
                source = "MockAnalysisOrchestrator",
                sqlFacts,
                deterministicScore = scoring,
                retrievedChunks = retrievedChunks.Select(chunk => new
                {
                    sourceId = chunk.SourceId,
                    score = chunk.Score,
                    text = chunk.Text
                })
            }),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }
}
