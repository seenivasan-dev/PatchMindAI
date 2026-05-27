using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Infrastructure.Services;

public sealed class CveKnowledgeRetriever : IKnowledgeRetriever
{
    private readonly INvdClient _nvdClient;

    public CveKnowledgeRetriever(INvdClient nvdClient)
    {
        _nvdClient = nvdClient;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        return await _nvdClient.RetrieveAsync(query, topK, cancellationToken);
    }
}