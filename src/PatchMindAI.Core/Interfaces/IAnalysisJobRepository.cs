using PatchMindAI.Core.Domain;

namespace PatchMindAI.Core.Interfaces;

public interface IAnalysisJobRepository
{
    Task CreateAsync(AnalysisJob job, CancellationToken cancellationToken = default);

    Task<AnalysisJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task UpdateAsync(AnalysisJob job, CancellationToken cancellationToken = default);
}
