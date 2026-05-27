using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using PatchMindAI.Core.Configuration;

namespace PatchMindAI.API.Health;

public sealed class ProviderConfigurationHealthCheck : IHealthCheck
{
    private readonly ServiceBusOptions _serviceBus;
    private readonly RedisOptions _redis;
    private readonly AzureOpenAIOptions _azureOpenAI;
    private readonly AzureSearchOptions _azureSearch;

    public ProviderConfigurationHealthCheck(
        IOptions<ServiceBusOptions> serviceBus,
        IOptions<RedisOptions> redis,
        IOptions<AzureOpenAIOptions> azureOpenAI,
        IOptions<AzureSearchOptions> azureSearch)
    {
        _serviceBus = serviceBus.Value;
        _redis = redis.Value;
        _azureOpenAI = azureOpenAI.Value;
        _azureSearch = azureSearch.Value;
    }

    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var issues = new List<string>();

        if (_serviceBus.Provider.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(_serviceBus.FullyQualifiedNamespace)
            && string.IsNullOrWhiteSpace(_serviceBus.ConnectionString))
        {
            issues.Add("ServiceBus provider is AzureServiceBus but both FullyQualifiedNamespace and ConnectionString are empty.");
        }

        if (_redis.Provider.Equals("Redis", StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(_redis.ConnectionString))
        {
            issues.Add("Redis provider is Redis but ConnectionString is empty.");
        }

        if (!string.IsNullOrWhiteSpace(_azureOpenAI.Endpoint) ^ !string.IsNullOrWhiteSpace(_azureOpenAI.DeploymentName))
        {
            issues.Add("AzureOpenAI requires both Endpoint and DeploymentName when configured.");
        }

        if (!string.IsNullOrWhiteSpace(_azureOpenAI.Endpoint)
            && !string.IsNullOrWhiteSpace(_azureOpenAI.DeploymentName)
            && string.IsNullOrWhiteSpace(_azureOpenAI.ApiKey)
            && !_azureOpenAI.UseManagedIdentity)
        {
            issues.Add("AzureOpenAI requires ApiKey when UseManagedIdentity is false.");
        }

        if (!string.IsNullOrWhiteSpace(_azureSearch.Endpoint) ^ !string.IsNullOrWhiteSpace(_azureSearch.IndexName))
        {
            issues.Add("AzureSearch requires both Endpoint and IndexName when configured.");
        }

        if (!string.IsNullOrWhiteSpace(_azureSearch.Endpoint)
            && !string.IsNullOrWhiteSpace(_azureSearch.IndexName)
            && string.IsNullOrWhiteSpace(_azureSearch.ApiKey)
            && !_azureSearch.UseManagedIdentity)
        {
            issues.Add("AzureSearch requires ApiKey when UseManagedIdentity is false.");
        }

        if (issues.Count > 0)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy(string.Join(" ", issues)));
        }

        return Task.FromResult(HealthCheckResult.Healthy("Provider configuration is valid."));
    }
}
