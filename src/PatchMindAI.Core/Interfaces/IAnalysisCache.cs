using PatchMindAI.Core.Domain;

namespace PatchMindAI.Core.Interfaces;

public interface IAnalysisCache
{
    Task SetJobAsync(AnalysisJob job, CancellationToken cancellationToken = default);

    Task<AnalysisJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task SetResultAsync(AnalysisResult result, CancellationToken cancellationToken = default);

    Task<AnalysisResult?> GetResultAsync(Guid jobId, CancellationToken cancellationToken = default);
}
