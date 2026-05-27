using PatchMindAI.Core.Domain;

namespace PatchMindAI.Core.Interfaces;

public interface IAnalysisResultRepository
{
    Task SaveAsync(AnalysisResult result, CancellationToken cancellationToken = default);

    Task<AnalysisResult?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default);
}
