using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatchMindAI.Core.Domain;
using PatchMindAI.Infrastructure.Data;

namespace PatchMindAI.Infrastructure.SeedData;

/// <summary>
/// Seeds CVE data from the SQL database into Azure AI Search for knowledge retrieval.
/// </summary>
public sealed class AzureSearchSeeder
{
    private readonly SearchClient _searchClient;
    private readonly PatchMindDbContext _context;
    private readonly ILogger<AzureSearchSeeder> _logger;

    public AzureSearchSeeder(
        SearchClient searchClient,
        PatchMindDbContext context,
        ILogger<AzureSearchSeeder> logger)
    {
        _searchClient = searchClient;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Seeds CVE documents from SQL database to Azure Search index.
    /// Skips seeding if documents already exist in the index.
    /// </summary>
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            // 1. Check if index already has documents
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

            // 2. Fetch all CVEs from SQL database
            var cves = await _context.Cves.ToListAsync(cancellationToken);

            if (cves.Count == 0)
            {
                _logger.LogWarning("No CVEs found in database to seed");
                return;
            }

            // 3. Map CVE entities to SearchDocument format
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

            // 4. Batch upload to Azure Search (max 1000 docs per batch)
            var batches = documents.Chunk(1000);
            var totalUploaded = 0;

            foreach (var batch in batches)
            {
                var uploadResult = await _searchClient.UploadDocumentsAsync(
                    batch,
                    cancellationToken: cancellationToken);

                totalUploaded += batch.Length;
                _logger.LogInformation(
                    "Uploaded batch of {BatchSize} documents ({Total}/{TotalCount})",
                    batch.Length, totalUploaded, documents.Count);
            }

            _logger.LogInformation(
                "Azure Search seeding completed. Uploaded {Count} CVE documents",
                cves.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to seed Azure Search index. The application will continue, but knowledge retrieval may return empty results.");
            // Don't throw - allow app to start even if Azure Search seeding fails
        }
    }

    /// <summary>
    /// Builds a searchable title from the CVE's first sentence.
    /// </summary>
    private static string BuildTitle(Cve cve)
    {
        if (string.IsNullOrWhiteSpace(cve.Description))
        {
            return cve.Id;
        }

        // Use first sentence of description as title
        var sentences = cve.Description.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var firstSentence = sentences.Length > 0 ? sentences[0] : cve.Description;

        // Truncate if too long
        if (firstSentence.Length > 150)
        {
            firstSentence = firstSentence[..147] + "...";
        }

        return $"{cve.Id}: {firstSentence}";
    }

    /// <summary>
    /// Builds rich searchable text combining all relevant CVE fields.
    /// </summary>
    private static string BuildContentText(Cve cve)
    {
        var parts = new List<string>
        {
            $"{cve.Id}: {cve.Description}"
        };

        // Add severity and score
        parts.Add($"Severity: {cve.Severity}.");
        parts.Add($"CVSS Base Score: {cve.BaseScore}.");

        // Add vector string if available (e.g., "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:C/C:H/I:H/A:H")
        if (!string.IsNullOrWhiteSpace(cve.VectorString))
        {
            parts.Add($"CVSS Vector: {cve.VectorString}.");
        }

        // Add affected products
        if (cve.AffectedProducts?.Length > 0)
        {
            parts.Add($"Affected Products: {string.Join(", ", cve.AffectedProducts)}.");
        }

        // Add weaknesses
        if (cve.Weaknesses?.Length > 0)
        {
            parts.Add($"Weaknesses: {string.Join(", ", cve.Weaknesses)}.");
        }

        // Add references
        if (cve.References?.Length > 0)
        {
            var limitedRefs = cve.References.Take(5); // Limit to first 5 to avoid bloat
            parts.Add($"References: {string.Join("; ", limitedRefs)}.");
        }

        // Add published date context
        parts.Add($"Published: {cve.PublishedAtUtc:yyyy-MM-dd}.");

        return string.Join(" ", parts);
    }
}
