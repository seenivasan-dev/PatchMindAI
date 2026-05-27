namespace PatchMindAI.Core.Interfaces;

public interface IKnowledgeRetriever
{
    Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int topK = 5, CancellationToken cancellationToken = default);
}

public sealed class RetrievedChunk
{
    public string SourceId { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public double Score { get; set; }
}
