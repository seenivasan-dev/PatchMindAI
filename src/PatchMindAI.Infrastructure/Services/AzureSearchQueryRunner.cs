using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Core.Interfaces;
using System.Text.Json;

namespace PatchMindAI.Infrastructure.Services;

public sealed class AzureSearchQueryRunner : IAzureSearchQueryRunner
{
    private readonly SearchClient _searchClient;
    private readonly AzureSearchOptions _options;

    public AzureSearchQueryRunner(SearchClient searchClient, IOptions<AzureSearchOptions> options)
    {
        _searchClient = searchClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> SearchAsync(string? searchText, SearchOptions searchOptions, CancellationToken cancellationToken = default)
    {
        var response = await _searchClient.SearchAsync<SearchDocument>(searchText, searchOptions, cancellationToken);
        var chunks = new List<RetrievedChunk>();

        await foreach (var result in response.Value.GetResultsAsync().WithCancellation(cancellationToken))
        {
            var document = result.Document;
            var sourceId = GetString(document, _options.SourceIdField) ?? "search-result";
            var content = GetString(document, _options.ContentField) ?? document.ToString();
            var title = string.IsNullOrWhiteSpace(_options.TitleField) ? null : GetString(document, _options.TitleField);

            var snippet = string.IsNullOrWhiteSpace(title)
                ? content
                : $"{title}: {content}";

            chunks.Add(new RetrievedChunk
            {
                SourceId = sourceId,
                Text = snippet,
                Score = result.Score ?? 0
            });
        }

        return chunks;
    }

    private static string? GetString(SearchDocument document, string fieldName)
    {
        if (document.TryGetValue(fieldName, out var value) && value is not null)
        {
            return value switch
            {
                string text => text,
                JsonElement json when json.ValueKind == JsonValueKind.String => json.GetString(),
                JsonElement json => json.ToString(),
                _ => value.ToString()
            };
        }

        return null;
    }
}
