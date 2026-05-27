using System.Collections.Concurrent;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Infrastructure.Repositories;

public sealed class InMemoryAnalysisResultRepository : IAnalysisResultRepository
{
    private readonly ConcurrentDictionary<Guid, AnalysisResult> _resultsByJobId = new();

    public Task SaveAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        _resultsByJobId[result.JobId] = Clone(result);
        return Task.CompletedTask;
    }

    public Task<AnalysisResult?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var found = _resultsByJobId.TryGetValue(jobId, out var result) ? Clone(result) : null;
        return Task.FromResult(found);
    }

    private static AnalysisResult Clone(AnalysisResult source)
    {
        return new AnalysisResult
        {
            Id = source.Id,
            JobId = source.JobId,
            RiskScore = source.RiskScore,
            RiskJustification = source.RiskJustification,
            ImpactSummary = source.ImpactSummary,
            AffectedAssetsJson = source.AffectedAssetsJson,
            RemediationStepsJson = source.RemediationStepsJson,
            RawAgentOutputJson = source.RawAgentOutputJson,
            GeneratedAtUtc = source.GeneratedAtUtc
        };
    }
}
