using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Infrastructure.Services;

public interface IAzureSearchQueryRunner
{
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(string? searchText, SearchOptions searchOptions, CancellationToken cancellationToken = default);
}
