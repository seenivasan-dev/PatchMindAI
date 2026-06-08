using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PatchMindAI.Agents;
using PatchMindAI.API;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Infrastructure.Services;

namespace PatchMindAI.Tests.Integration.Workflows;

public sealed class PromptAnalysisCitationsWorkflowTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;
    private string? _dbPath;

    public Task InitializeAsync()
    {
        _dbPath = Path.Combine(Path.GetTempPath(), $"patchmindai-integration-{Guid.NewGuid():N}.db");

        var overrides = new Dictionary<string, string?>
        {
            ["ConnectionStrings:PatchMindAIDb"] = $"Data Source={_dbPath}",
            ["ServiceBus:Provider"] = "InMemory",
            ["Redis:Provider"] = "InMemory",
            ["AzureOpenAI:Endpoint"] = string.Empty,
            ["AzureOpenAI:DeploymentName"] = string.Empty,
            ["AzureSearch:Endpoint"] = string.Empty,
            ["AzureSearch:IndexName"] = string.Empty
        };

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureAppConfiguration((_, configuration) =>
                {
                    configuration.Sources.Clear();
                    configuration.AddInMemoryCollection(overrides);
                });

                builder.ConfigureServices(services =>
                {
                    services.AddScoped<IAnalysisOrchestrator, MockAnalysisOrchestrator>();
                    services.AddScoped<IKnowledgeRetriever, CveKnowledgeRetriever>();
                });
            });

        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();

        if (!string.IsNullOrWhiteSpace(_dbPath) && File.Exists(_dbPath))
        {
            File.Delete(_dbPath);
        }

        return Task.CompletedTask;
    }

    [Fact]
    public async Task PromptFlow_ShouldReturnCompletedResult_WithCitationPayloadForUi()
    {
        // Arrange
        var request = new
        {
            question = "Analyze CVE-2021-44228 and list key remediation steps."
        };

        // Act
        using var createResponse = await _client!.PostAsJsonAsync("/api/analysis/prompts", request);

        // Assert create
        Assert.Equal(HttpStatusCode.Accepted, createResponse.StatusCode);

        using var createJson = JsonDocument.Parse(await createResponse.Content.ReadAsStringAsync());
        var jobId = createJson.RootElement.GetProperty("jobId").GetGuid();

        var status = string.Empty;
        for (var attempt = 0; attempt < 30; attempt++)
        {
            using var statusResponse = await _client.GetAsync($"/api/analysis/jobs/{jobId}/status");
            statusResponse.EnsureSuccessStatusCode();

            using var statusJson = JsonDocument.Parse(await statusResponse.Content.ReadAsStringAsync());
            status = statusJson.RootElement.GetProperty("status").GetString() ?? string.Empty;

            if (status.Equals("Completed", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (status.Equals("Failed", StringComparison.OrdinalIgnoreCase))
            {
                var failureReason = statusJson.RootElement.TryGetProperty("failureReason", out var reason)
                    ? reason.GetString()
                    : "Unknown failure";
                throw new Xunit.Sdk.XunitException($"Job failed unexpectedly: {failureReason}");
            }

            await Task.Delay(500);
        }

        Assert.Equal("Completed", status);

        // Verify prompt-alias status endpoint contract
        using var promptStatusResponse = await _client.GetAsync($"/api/analysis/prompts/{jobId}/status");
        Assert.Equal(HttpStatusCode.OK, promptStatusResponse.StatusCode);

        using var promptStatusJson = JsonDocument.Parse(await promptStatusResponse.Content.ReadAsStringAsync());
        var promptStatus = promptStatusJson.RootElement.GetProperty("status").GetString();
        Assert.Equal("Completed", promptStatus);

        using var resultResponse = await _client.GetAsync($"/api/analysis/jobs/{jobId}/result");
        Assert.Equal(HttpStatusCode.OK, resultResponse.StatusCode);

        // Verify prompt-alias result endpoint contract
        using var promptResultResponse = await _client.GetAsync($"/api/analysis/prompts/{jobId}/result");
        Assert.Equal(HttpStatusCode.OK, promptResultResponse.StatusCode);

        using var resultJson = JsonDocument.Parse(await resultResponse.Content.ReadAsStringAsync());
        var rawAgentOutputJson = resultJson.RootElement.GetProperty("rawAgentOutputJson").GetString();

        Assert.False(string.IsNullOrWhiteSpace(rawAgentOutputJson));

        using var rawJson = JsonDocument.Parse(rawAgentOutputJson!);
        Assert.True(rawJson.RootElement.TryGetProperty("retrievedChunks", out var retrievedChunks));
        Assert.Equal(JsonValueKind.Array, retrievedChunks.ValueKind);
        Assert.True(retrievedChunks.GetArrayLength() > 0);

        var firstCitation = retrievedChunks[0];

        var hasSourceId = firstCitation.TryGetProperty("sourceId", out var sourceId)
            || firstCitation.TryGetProperty("SourceId", out sourceId);
        var hasScore = firstCitation.TryGetProperty("score", out var score)
            || firstCitation.TryGetProperty("Score", out score);
        var hasText = firstCitation.TryGetProperty("text", out var text)
            || firstCitation.TryGetProperty("Text", out text);

        Assert.True(hasSourceId);
        Assert.True(hasScore);
        Assert.True(hasText);
        Assert.Equal(JsonValueKind.String, sourceId.ValueKind);
        Assert.Equal(JsonValueKind.String, text.ValueKind);
        Assert.True(score.ValueKind is JsonValueKind.Number or JsonValueKind.String);
    }
}
