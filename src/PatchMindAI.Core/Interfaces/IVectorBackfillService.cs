namespace PatchMindAI.Core.Interfaces;

public interface IVectorBackfillService
{
    bool IsAvailable { get; }

    Task<int> BackfillAsync(CancellationToken cancellationToken = default);
}
