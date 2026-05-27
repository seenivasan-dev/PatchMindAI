using PatchMindAI.Core.Domain;

namespace PatchMindAI.Core.Interfaces;

public interface INvdClient
{
    Task<Cve?> GetCveByIdAsync(string cveId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Cve>> SearchAsync(string keyword, int limit = 20, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int topK = 5, CancellationToken cancellationToken = default);
}
