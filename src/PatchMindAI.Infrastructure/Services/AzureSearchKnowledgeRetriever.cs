using Azure;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Options;
using System.Text.Json;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Infrastructure.Services;

public sealed class AzureSearchKnowledgeRetriever : IKnowledgeRetriever
{
    private readonly SearchClient _searchClient;
    private readonly AzureSearchOptions _options;

    public AzureSearchKnowledgeRetriever(SearchClient searchClient, IOptions<AzureSearchOptions> options)
    {
        _searchClient = searchClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        var searchOptions = new SearchOptions
        {
            Size = Math.Clamp(topK, 1, 20),
            QueryType = SearchQueryType.Simple,
            IncludeTotalCount = false
        };

        searchOptions.Select.Add(_options.SourceIdField);
        searchOptions.Select.Add(_options.ContentField);

        if (!string.IsNullOrWhiteSpace(_options.TitleField))
        {
            searchOptions.Select.Add(_options.TitleField);
        }

        var response = await _searchClient.SearchAsync<SearchDocument>(string.IsNullOrWhiteSpace(query) ? "*" : query, searchOptions, cancellationToken);
        var results = new List<RetrievedChunk>();

        await foreach (var result in response.Value.GetResultsAsync().WithCancellation(cancellationToken))
        {
            var document = result.Document;
            var sourceId = GetString(document, _options.SourceIdField) ?? "search-result";
            var content = GetString(document, _options.ContentField) ?? document.ToString();
            var title = string.IsNullOrWhiteSpace(_options.TitleField) ? null : GetString(document, _options.TitleField);

            var snippet = string.IsNullOrWhiteSpace(title)
                ? content
                : $"{title}: {content}";

            results.Add(new RetrievedChunk
            {
                SourceId = sourceId,
                Text = snippet,
                Score = result.Score ?? 0
            });
        }

        return results;
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