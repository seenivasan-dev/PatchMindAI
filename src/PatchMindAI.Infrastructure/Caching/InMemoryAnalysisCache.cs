using System.Collections.Concurrent;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Infrastructure.Caching;

public sealed class InMemoryAnalysisCache : IAnalysisCache
{
    private readonly ConcurrentDictionary<Guid, AnalysisJob> _jobs = new();
    private readonly ConcurrentDictionary<Guid, AnalysisResult> _results = new();

    public Task SetJobAsync(AnalysisJob job, CancellationToken cancellationToken = default)
    {
        _jobs[job.Id] = Clone(job);
        return Task.CompletedTask;
    }

    public Task<AnalysisJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var value = _jobs.TryGetValue(jobId, out var job) ? Clone(job) : null;
        return Task.FromResult(value);
    }

    public Task SetResultAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        _results[result.JobId] = Clone(result);
        return Task.CompletedTask;
    }

    public Task<AnalysisResult?> GetResultAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var value = _results.TryGetValue(jobId, out var result) ? Clone(result) : null;
        return Task.FromResult(value);
    }

    private static AnalysisJob Clone(AnalysisJob source)
    {
        return new AnalysisJob
        {
            Id = source.Id,
            CveId = source.CveId,
            UserQuery = source.UserQuery,
            Status = source.Status,
            CreatedAtUtc = source.CreatedAtUtc,
            CompletedAtUtc = source.CompletedAtUtc,
            FailureReason = source.FailureReason
        };
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
