using Azure;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using PatchMindAI.Core.Domain;
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

            // 2. Check if index already has documents
            var countResult = await _searchClient.SearchAsync<SearchDocument>("*",
                new SearchOptions { Size = 0, IncludeTotalCount = true },
                cancellationToken);

            if (countResult.Value.TotalCount > 0)
            {
                _logger.LogInformation(
                    "Azure Search index already has {Count} documents, skipping seed",
                    countResult.Value.TotalCount);
                return;
            }

            _logger.LogInformation("Starting Azure Search index seeding...");

            // 3. Fetch all CVEs from SQL database
            var cves = await _context.Cves.ToListAsync(cancellationToken);

            if (cves.Count == 0)
            {
                _logger.LogWarning("No CVEs found in database to seed");
                return;
            }

            // 4. Map CVE entities to SearchDocument format
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

            // 5. Batch upload to Azure Search (max 1000 docs per batch)
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

    private async Task EnsureIndexExistsAsync(CancellationToken cancellationToken)
    {
        var indexName = _configuration["AzureSearch:IndexName"]!;

        try
        {
            // Check if index exists
            await _indexClient.GetIndexAsync(indexName, cancellationToken);
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

            await _indexClient.CreateIndexAsync(index, cancellationToken);
            _logger.LogInformation("Successfully created Azure Search index '{IndexName}'", indexName);
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
}