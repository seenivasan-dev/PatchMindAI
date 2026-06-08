using System.Text.Json;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using PatchMindAI.Core.Configuration;

namespace PatchMindAI.API.Health;

public sealed class VectorCoverageHealthCheck : IHealthCheck
{
    private readonly SearchClient _searchClient;
    private readonly AzureSearchOptions _options;

    public VectorCoverageHealthCheck(
        SearchClient searchClient,
        IOptions<AzureSearchOptions> options)
    {
        _searchClient = searchClient;
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (!_options.EnableVectorSearch
            || string.IsNullOrWhiteSpace(_options.Endpoint)
            || string.IsNullOrWhiteSpace(_options.IndexName))
        {
            return HealthCheckResult.Healthy("Vector coverage check skipped because vector search is not enabled.");
        }

        var sampleSize = Math.Clamp(_options.VectorCoverageSampleSize, 10, 5000);
        var searchOptions = new SearchOptions
        {
            Size = sampleSize,
            IncludeTotalCount = true
        };

        searchOptions.Select.Add("id");
        searchOptions.Select.Add(_options.VectorField);

        var response = await _searchClient.SearchAsync<SearchDocument>("*", searchOptions, cancellationToken);

        var sampled = 0;
        var withVector = 0;

        await foreach (var result in response.Value.GetResultsAsync().WithCancellation(cancellationToken))
        {
            sampled++;
            if (HasVector(result.Document, _options.VectorField))
            {
                withVector++;
            }
        }

        if (sampled == 0)
        {
            return HealthCheckResult.Healthy("Vector coverage check passed because no search documents are currently indexed.");
        }

        var coverage = (double)withVector / sampled;
        var threshold = Math.Clamp(_options.VectorCoverageReadinessThreshold, 0, 1);

        var data = new Dictionary<string, object>
        {
            ["sampledDocuments"] = sampled,
            ["documentsWithVector"] = withVector,
            ["coverage"] = coverage,
            ["threshold"] = threshold,
            ["totalDocuments"] = response.Value.TotalCount ?? 0L
        };

        if (coverage < threshold)
        {
            return HealthCheckResult.Unhealthy(
                $"Vector coverage {coverage:P1} is below threshold {threshold:P1}.",
                data: data);
        }

        return HealthCheckResult.Healthy($"Vector coverage {coverage:P1} meets threshold {threshold:P1}.", data);
    }

    private static bool HasVector(SearchDocument document, string vectorField)
    {
        if (!document.TryGetValue(vectorField, out var value) || value is null)
        {
            return false;
        }

        return value switch
        {
            ReadOnlyMemory<float> memory => memory.Length > 0,
            float[] array => array.Length > 0,
            IEnumerable<float> enumerable => enumerable.Any(),
            JsonElement json when json.ValueKind == JsonValueKind.Array => json.GetArrayLength() > 0,
            JsonElement json when json.ValueKind == JsonValueKind.String => !string.IsNullOrWhiteSpace(json.GetString()),
            _ => !string.IsNullOrWhiteSpace(value.ToString())
        };
    }
}
