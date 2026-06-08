using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using PatchMindAI.Core.Configuration;

namespace PatchMindAI.API.Health;

public sealed class AzureOpenAiConnectivityHealthCheck : IHealthCheck
{
    private static readonly HttpClient HttpClient = new()
    {
        Timeout = TimeSpan.FromSeconds(5)
    };

    private readonly AzureOpenAIOptions _options;

    public AzureOpenAiConnectivityHealthCheck(IOptions<AzureOpenAIOptions> options)
    {
        _options = options.Value;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Endpoint) || string.IsNullOrWhiteSpace(_options.DeploymentName))
        {
            return HealthCheckResult.Healthy("Azure OpenAI check skipped because endpoint/deployment are not configured.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, _options.Endpoint);
            using var response = await HttpClient.SendAsync(request, cancellationToken);

            return HealthCheckResult.Healthy($"Azure OpenAI endpoint reachable (HTTP {(int)response.StatusCode}).");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Azure OpenAI endpoint is unreachable.", ex);
        }
    }
}
