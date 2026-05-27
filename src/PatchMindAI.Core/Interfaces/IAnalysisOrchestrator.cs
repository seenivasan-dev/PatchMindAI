using PatchMindAI.Core.Domain;

namespace PatchMindAI.Core.Interfaces;

public interface IAnalysisOrchestrator
{
    Task<AnalysisResult> RunAsync(AnalysisJob job, CancellationToken cancellationToken = default);
}
