using Azure;
using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using OpenAI.Embeddings;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Infrastructure.Data;

namespace PatchMindAI.Infrastructure.SeedData;

/// <summary>
/// Seeds CVE data from the SQL database into Azure AI Search for knowledge retrieval.
/// Automatically creates the index if it doesn't exist.
/// </summary>
public sealed class AzureSearchSeeder
{
    private readonly SearchClient _searchClient;
    private readonly SearchIndexClient _indexClient;
    private readonly PatchMindDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureSearchSeeder> _logger;
    private readonly AzureSearchOptions _searchOptions;

    public AzureSearchSeeder(
        SearchClient searchClient,
        SearchIndexClient indexClient,
        PatchMindDbContext context,
        IConfiguration configuration,
        ILogger<AzureSearchSeeder> logger)
    {
        _searchClient = searchClient;
        _indexClient = indexClient;
        _context = context;
        _configuration = configuration;
        _logger = logger;
        _searchOptions = configuration.GetSection(AzureSearchOptions.SectionName).Get<AzureSearchOptions>()
            ?? new AzureSearchOptions();
    }

    /// <summary>
    /// Ensures the index exists and seeds CVE documents from SQL database.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Ensure index exists
            await EnsureIndexExistsAsync(cancellationToken);

            // 2. Fetch all CVEs from SQL database first
            var cves = await _context.Cves.ToListAsync(cancellationToken);

            if (cves.Count == 0)
            {
                _logger.LogWarning("No CVEs found in database to seed");
                return;
            }

            // 3. Check if index already has correct number of documents
            var countResult = await _searchClient.SearchAsync<SearchDocument>("*",
                new SearchOptions { Size = 0, IncludeTotalCount = true },
                cancellationToken);

            if (countResult.Value.TotalCount == cves.Count)
            {
                _logger.LogInformation(
                    "Azure Search index already has {Count} documents matching database, skipping seed",
                    countResult.Value.TotalCount);
                return;
            }

            // 4. If counts don't match, delete all existing documents and re-seed
            if (countResult.Value.TotalCount > 0)
            {
                _logger.LogInformation(
                    "Azure Search has {SearchCount} documents but database has {DbCount} CVEs. Clearing index...",
                    countResult.Value.TotalCount, cves.Count);

                await DeleteAllDocumentsAsync(cancellationToken);
            }

            _logger.LogInformation("Starting Azure Search index seeding with {Count} CVEs...", cves.Count);

            // 5. Map CVE entities to SearchDocument format
            var documents = cves.Select(cve => new SearchDocument
            {
                ["id"] = cve.Id,
                ["cveId"] = cve.Id,
                ["title"] = BuildTitle(cve),
                ["content"] = BuildContentText(cve),
                ["severity"] = cve.Severity.ToString(),
                ["baseScore"] = cve.BaseScore,
                ["publishedAtUtc"] = cve.PublishedAtUtc,
                ["lastModifiedAtUtc"] = cve.LastModifiedAtUtc
            }).ToList();

            // 6. Batch upload to Azure Search (max 1000 docs per batch)
            var batches = documents.Chunk(1000);
            var totalUploaded = 0;

            foreach (var batch in batches)
            {
                var uploadResult = await _searchClient.UploadDocumentsAsync(
                    batch,
                    cancellationToken: cancellationToken);

                totalUploaded += uploadResult.Value.Results.Count;
            }

            _logger.LogInformation(
                "Successfully seeded {Count} CVE documents to Azure Search",
                totalUploaded);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to seed Azure Search index");
            throw;
        }
    }

    /// <summary>
    /// Backfills vector embeddings for indexed CVE documents.
    /// </summary>
    public async Task<int> BackfillVectorsAsync(CancellationToken cancellationToken = default)
    {
        if (!_searchOptions.EnableVectorSearch)
        {
            _logger.LogInformation("Vector backfill skipped because EnableVectorSearch is false.");
            return 0;
        }

        if (string.IsNullOrWhiteSpace(_searchOptions.AzureOpenAIEndpoint)
            || string.IsNullOrWhiteSpace(_searchOptions.AzureOpenAIEmbeddingDeployment))
        {
            _logger.LogWarning("Vector backfill skipped because AzureOpenAI endpoint or embedding deployment is not configured.");
            return 0;
        }

        await EnsureIndexExistsAsync(cancellationToken);

        var embeddingClient = CreateEmbeddingClient();
        var cves = await _context.Cves.ToListAsync(cancellationToken);
        if (cves.Count == 0)
        {
            _logger.LogInformation("Vector backfill skipped because no CVEs were found in database.");
            return 0;
        }

        var batchSize = Math.Clamp(_searchOptions.VectorBackfillBatchSize, 1, 128);
        var updated = 0;

        foreach (var batch in cves.Chunk(batchSize))
        {
            var contentBatch = batch.Select(BuildContentText).ToArray();
            var embeddingsResponse = await embeddingClient.GenerateEmbeddingsAsync(contentBatch, cancellationToken: cancellationToken);

            var vectorDocs = new List<SearchDocument>(batch.Length);
            var index = 0;
            foreach (var embedding in embeddingsResponse.Value)
            {
                var cve = batch[index++];
                vectorDocs.Add(new SearchDocument
                {
                    ["id"] = cve.Id,
                    [_searchOptions.VectorField] = embedding.ToFloats().ToArray()
                });
            }

            if (vectorDocs.Count > 0)
            {
                var mergeResult = await _searchClient.MergeOrUploadDocumentsAsync(vectorDocs, cancellationToken: cancellationToken);
                updated += mergeResult.Value.Results.Count;
            }
        }

        _logger.LogInformation("Vector backfill completed. Updated {Count} documents with embeddings.", updated);
        return updated;
    }

    private async Task DeleteAllDocumentsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Search for all document IDs
            var searchOptions = new SearchOptions
            {
                Size = 1000,
                Select = { "id" }
            };

            var searchResults = await _searchClient.SearchAsync<SearchDocument>("*", searchOptions, cancellationToken);
            var documentsToDelete = new List<SearchDocument>();

            await foreach (var result in searchResults.Value.GetResultsAsync())
            {
                documentsToDelete.Add(new SearchDocument { ["id"] = result.Document["id"] });
            }

            if (documentsToDelete.Count > 0)
            {
                await _searchClient.DeleteDocumentsAsync(documentsToDelete, cancellationToken: cancellationToken);
                _logger.LogInformation("Deleted {Count} documents from Azure Search index", documentsToDelete.Count);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete documents from Azure Search index");
            throw;
        }
    }

    private async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
    {
        var indexName = _configuration["AzureSearch:IndexName"]!;

        try
        {
            // Check if index exists and patch in vector settings when missing.
            var existing = await _indexClient.GetIndexAsync(indexName, cancellationToken);
            var existingIndex = existing.Value;

            var needsVectorField = existingIndex.Fields.All(field => !field.Name.Equals(_searchOptions.VectorField, StringComparison.OrdinalIgnoreCase));
            var needsVectorProfile = existingIndex.VectorSearch?.Profiles.All(profile => !profile.Name.Equals(_searchOptions.VectorProfileName, StringComparison.OrdinalIgnoreCase)) ?? true;

            if (needsVectorField || needsVectorProfile)
            {
                _logger.LogInformation("Updating Azure Search index '{IndexName}' with vector search schema.", indexName);
                EnsureVectorSchema(existingIndex);
                await _indexClient.CreateOrUpdateIndexAsync(existingIndex, cancellationToken: cancellationToken);
            }

            _logger.LogInformation("Azure Search index '{IndexName}' already exists", indexName);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            // Index doesn't exist, create it
            _logger.LogInformation("Creating Azure Search index '{IndexName}'...", indexName);

            var index = new SearchIndex(indexName)
            {
                Fields =
                {
                    new SimpleField("id", SearchFieldDataType.String) { IsKey = true, IsFilterable = true },
                    new SearchableField("cveId") { IsFilterable = true },
                    new SearchableField("title"),
                    new SearchableField("content"),
                    new SimpleField("severity", SearchFieldDataType.String) { IsFilterable = true, IsFacetable = true },
                    new SimpleField("baseScore", SearchFieldDataType.Double) { IsFilterable = true, IsSortable = true },
                    new SimpleField("publishedAtUtc", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true },
                    new SimpleField("lastModifiedAtUtc", SearchFieldDataType.DateTimeOffset) { IsFilterable = true, IsSortable = true }
                }
            };

            EnsureVectorSchema(index);

            await _indexClient.CreateIndexAsync(index, cancellationToken);
            _logger.LogInformation("Successfully created Azure Search index '{IndexName}'", indexName);
        }
    }

    private void EnsureVectorSchema(SearchIndex index)
    {
        if (!_searchOptions.EnableVectorSearch)
        {
            return;
        }

        if (index.Fields.All(field => !field.Name.Equals(_searchOptions.VectorField, StringComparison.OrdinalIgnoreCase)))
        {
            index.Fields.Add(new SearchField(_searchOptions.VectorField, SearchFieldDataType.Collection(SearchFieldDataType.Single))
            {
                IsSearchable = true,
                VectorSearchDimensions = _searchOptions.VectorDimensions,
                VectorSearchProfileName = _searchOptions.VectorProfileName
            });
        }

        index.VectorSearch ??= new VectorSearch();

        if (index.VectorSearch.Algorithms.All(algorithm => !algorithm.Name.Equals(_searchOptions.VectorAlgorithmName, StringComparison.OrdinalIgnoreCase)))
        {
            index.VectorSearch.Algorithms.Add(new HnswAlgorithmConfiguration(_searchOptions.VectorAlgorithmName));
        }

        var hasVectorizerConfig = !string.IsNullOrWhiteSpace(_searchOptions.AzureOpenAIEndpoint)
            && !string.IsNullOrWhiteSpace(_searchOptions.AzureOpenAIEmbeddingDeployment);

        if (hasVectorizerConfig)
        {
            var existingVectorizer = index.VectorSearch.Vectorizers
                .FirstOrDefault(vectorizer => vectorizer.VectorizerName.Equals(_searchOptions.VectorizerName, StringComparison.OrdinalIgnoreCase));

            var embeddingModelName = string.IsNullOrWhiteSpace(_searchOptions.AzureOpenAIEmbeddingModelName)
                ? _searchOptions.AzureOpenAIEmbeddingDeployment!
                : _searchOptions.AzureOpenAIEmbeddingModelName!;

            var vectorizer = new AzureOpenAIVectorizer(_searchOptions.VectorizerName)
            {
                Parameters = new AzureOpenAIVectorizerParameters
                {
                    ResourceUri = new Uri(_searchOptions.AzureOpenAIEndpoint!),
                    DeploymentName = _searchOptions.AzureOpenAIEmbeddingDeployment!,
                    ModelName = embeddingModelName,
                    ApiKey = string.IsNullOrWhiteSpace(_searchOptions.AzureOpenAIApiKey)
                        ? null
                        : _searchOptions.AzureOpenAIApiKey
                }
            };

            if (existingVectorizer is not null)
            {
                index.VectorSearch.Vectorizers.Remove(existingVectorizer);
            }

            index.VectorSearch.Vectorizers.Add(vectorizer);
        }

        if (index.VectorSearch.Profiles.All(profile => !profile.Name.Equals(_searchOptions.VectorProfileName, StringComparison.OrdinalIgnoreCase)))
        {
            if (hasVectorizerConfig)
            {
                var profile = new VectorSearchProfile(
                    _searchOptions.VectorProfileName,
                    _searchOptions.VectorAlgorithmName)
                {
                    VectorizerName = _searchOptions.VectorizerName
                };

                index.VectorSearch.Profiles.Add(profile);
            }
            else
            {
                index.VectorSearch.Profiles.Add(new VectorSearchProfile(
                    _searchOptions.VectorProfileName,
                    _searchOptions.VectorAlgorithmName));
            }
        }
    }

    private static string BuildTitle(Cve cve)
    {
        // Use first sentence of description as title
        var description = cve.Description ?? "";
        var firstSentence = description.Split('.').FirstOrDefault()?.Trim();
        return string.IsNullOrEmpty(firstSentence)
            ? cve.Id
            : $"{cve.Id}: {firstSentence}";
    }

    private static string BuildContentText(Cve cve)
    {
        var parts = new List<string>
        {
            $"CVE ID: {cve.Id}",
            $"Description: {cve.Description}",
            $"Severity: {cve.Severity}",
            $"CVSS Score: {cve.BaseScore}",
            $"Vector: {cve.VectorString}"
        };

        if (cve.AffectedProducts?.Any() == true)
        {
            parts.Add($"Affected Products: {string.Join(", ", cve.AffectedProducts)}");
        }

        if (cve.Weaknesses?.Any() == true)
        {
            parts.Add($"Weaknesses: {string.Join(", ", cve.Weaknesses)}");
        }

        if (cve.References?.Any() == true)
        {
            parts.Add($"References: {string.Join(", ", cve.References)}");
        }

        parts.Add($"Published: {cve.PublishedAtUtc:yyyy-MM-dd}");

        return string.Join("\n", parts);
    }

    private EmbeddingClient CreateEmbeddingClient()
    {
        var endpoint = new Uri(_searchOptions.AzureOpenAIEndpoint!);

        AzureOpenAIClient client;
        if (!string.IsNullOrWhiteSpace(_searchOptions.AzureOpenAIApiKey))
        {
            client = new AzureOpenAIClient(endpoint, new System.ClientModel.ApiKeyCredential(_searchOptions.AzureOpenAIApiKey));
        }
        else
        {
            client = new AzureOpenAIClient(endpoint, new DefaultAzureCredential());
        }

        return client.GetEmbeddingClient(_searchOptions.AzureOpenAIEmbeddingDeployment!);
    }
}