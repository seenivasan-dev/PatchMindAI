using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Infrastructure.Services;

public sealed class AzureSearchKnowledgeRetriever : IKnowledgeRetriever
{
    private readonly IAzureSearchQueryRunner _queryRunner;
    private readonly AzureSearchOptions _options;
    private readonly ILogger<AzureSearchKnowledgeRetriever> _logger;

    public AzureSearchKnowledgeRetriever(
        IAzureSearchQueryRunner queryRunner,
        IOptions<AzureSearchOptions> options,
        ILogger<AzureSearchKnowledgeRetriever> logger)
    {
        _queryRunner = queryRunner;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        var boundedTopK = Math.Clamp(topK, 1, 20);

        if (_options.EnableVectorSearch && !string.IsNullOrWhiteSpace(query))
        {
            try
            {
                var vectorOptions = CreateBaseSearchOptions(boundedTopK);
                var vectorQuery = new VectorizableTextQuery(query)
                {
                    KNearestNeighborsCount = boundedTopK
                };
                vectorQuery.Fields.Add(_options.VectorField);

                vectorOptions.VectorSearch = new VectorSearchOptions();
                vectorOptions.VectorSearch.Queries.Add(vectorQuery);

                var vectorResults = await _queryRunner.SearchAsync(null, vectorOptions, cancellationToken);
                if (vectorResults.Count > 0)
                {
                    _logger.LogDebug("Knowledge retrieval used vector search and returned {Count} chunks.", vectorResults.Count);
                    return vectorResults;
                }
            }
            catch (RequestFailedException ex)
            {
                _logger.LogWarning(ex, "Vector retrieval failed; falling back to lexical search.");
            }
        }

        var lexicalOptions = CreateBaseSearchOptions(boundedTopK);
        return await _queryRunner.SearchAsync(string.IsNullOrWhiteSpace(query) ? "*" : query, lexicalOptions, cancellationToken);
    }

    private SearchOptions CreateBaseSearchOptions(int topK)
    {
        var searchOptions = new SearchOptions
        {
            Size = topK,
            QueryType = SearchQueryType.Simple,
            IncludeTotalCount = false
        };

        searchOptions.Select.Add(_options.SourceIdField);
        searchOptions.Select.Add(_options.ContentField);

        if (!string.IsNullOrWhiteSpace(_options.TitleField))
        {
            searchOptions.Select.Add(_options.TitleField);
        }

        return searchOptions;
    }
}