using PatchMindAI.Core.Contracts;

namespace PatchMindAI.Core.Interfaces;

public interface IAnalysisJobQueue
{
    ValueTask EnqueueAsync(AnalysisRequestMessage request, CancellationToken cancellationToken = default);

    ValueTask<AnalysisRequestMessage> DequeueAsync(CancellationToken cancellationToken = default);
}
