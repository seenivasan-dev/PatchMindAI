using System.Collections.Concurrent;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Infrastructure.Repositories;

public sealed class InMemoryAnalysisJobRepository : IAnalysisJobRepository
{
    private readonly ConcurrentDictionary<Guid, AnalysisJob> _jobs = new();

    public Task CreateAsync(AnalysisJob job, CancellationToken cancellationToken = default)
    {
        _jobs[job.Id] = Clone(job);
        return Task.CompletedTask;
    }

    public Task<AnalysisJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var found = _jobs.TryGetValue(jobId, out var job) ? Clone(job) : null;
        return Task.FromResult(found);
    }

    public Task UpdateAsync(AnalysisJob job, CancellationToken cancellationToken = default)
    {
        _jobs[job.Id] = Clone(job);
        return Task.CompletedTask;
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
}
