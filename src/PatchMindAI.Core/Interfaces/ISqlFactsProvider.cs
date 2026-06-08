using PatchMindAI.Core.Models;

namespace PatchMindAI.Core.Interfaces;

public interface ISqlFactsProvider
{
    Task<SqlFactSnapshot> GetFactsForCveAsync(string cveId, int topAssets = 10, CancellationToken cancellationToken = default);
}
