using System.Text.Json;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Agents;

public sealed class MockAnalysisOrchestrator : IAnalysisOrchestrator
{
    private readonly INvdClient _nvdClient;
    private readonly IKnowledgeRetriever _knowledgeRetriever;

    public MockAnalysisOrchestrator(INvdClient nvdClient, IKnowledgeRetriever knowledgeRetriever)
    {
        _nvdClient = nvdClient;
        _knowledgeRetriever = knowledgeRetriever;
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

        var riskScore = Math.Max(cve.BaseScore, cve.Severity is SeverityLevel.Critical ? 9.5 : cve.BaseScore);
        var remediation = new[]
        {
            new { priority = "Critical", action = $"Patch affected products listed for {cve.Id}." },
            new { priority = "High", action = "Apply temporary mitigations and monitor exploit attempts." }
        };

        return new AnalysisResult
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            RiskScore = Math.Round(riskScore, 1),
            RiskJustification = $"Risk is derived from base score {cve.BaseScore} and severity {cve.Severity}.",
            ImpactSummary = $"{cve.Id} impacts {cve.AffectedProducts.Length} product groups. Immediate review is recommended.",
            AffectedAssetsJson = JsonSerializer.Serialize(cve.AffectedProducts),
            RemediationStepsJson = JsonSerializer.Serialize(remediation),
            RawAgentOutputJson = JsonSerializer.Serialize(new
            {
                planner = "mock",
                reflectionApplied = true,
                source = "MockAnalysisOrchestrator",
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
