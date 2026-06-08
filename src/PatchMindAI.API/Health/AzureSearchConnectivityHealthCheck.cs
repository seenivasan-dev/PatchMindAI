using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace PatchMindAI.API.Health;

public sealed class AzureSearchConnectivityHealthCheck : IHealthCheck
{
    private readonly SearchClient _searchClient;

    public AzureSearchConnectivityHealthCheck(SearchClient searchClient)
    {
        _searchClient = searchClient;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new SearchOptions
            {
                Size = 1,
                IncludeTotalCount = true
            };

            var response = await _searchClient.SearchAsync<SearchDocument>("*", options, cancellationToken);
            _ = response.Value.TotalCount;

            return HealthCheckResult.Healthy("Azure Search connectivity and index access are healthy.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Azure Search connectivity/index check failed.", ex);
        }
    }
}
