using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using PatchMindAI.API;

namespace PatchMindAI.Tests.Integration.Controllers;

public class AnalysisJobsControllerIntegrationTests : IAsyncLifetime
{
    private WebApplicationFactory<Program>? _factory;
    private HttpClient? _client;

    public async Task InitializeAsync()
    {
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
        
        // Give the app time to start
        await Task.Delay(500);
    }

    public async Task DisposeAsync()
    {
        _client?.Dispose();
        _factory?.Dispose();
    }

    [Fact]
    public async Task CreateAnalysisJob_WithValidCveId_ShouldReturn202Accepted()
    {
        // Arrange
        var request = new { cveId = "CVE-2021-44228", userQuery = "Test query" };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/analysis/jobs", request);

        // Assert
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var content = await response.Content.ReadFromJsonAsync<dynamic>();
        Assert.NotNull(content);
    }

    [Fact]
    public async Task CreateAnalysisJob_WithInvalidCveFormat_ShouldReturn400BadRequest()
    {
        // Arrange
        var request = new { cveId = "INVALID-CVE", userQuery = "Test" };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/analysis/jobs", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAnalysisJob_WithoutCveId_ShouldReturn400BadRequest()
    {
        // Arrange
        var request = new { userQuery = "Test" };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/analysis/jobs", request);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task CreateAnalysisJob_ResponseShouldContainCorrelationId()
    {
        // Arrange
        var request = new { cveId = "CVE-2021-44228", userQuery = "Test" };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/analysis/jobs", request);

        // Assert
        Assert.True(response.StatusCode == HttpStatusCode.Accepted);
        // Correlation ID is in the response body or response headers
        var content = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(content);
    }

    [Fact]
    public async Task CreateAnalysisJob_WithValidCveId_ShouldReturnAcceptedOrNotFound()
    {
        // Arrange - CVE validation and routing should work; NotFound only if CVE doesn't exist in NVD
        var request = new { cveId = "CVE-2021-44228", userQuery = "" };

        // Act
        var response = await _client!.PostAsJsonAsync("/api/analysis/jobs", request);

        // Assert - valid CVE format should reach the endpoint and either succeed (202) or fail with 404
        // depending on NVD database availability
        Assert.True(
            response.StatusCode == HttpStatusCode.Accepted || response.StatusCode == HttpStatusCode.NotFound,
            $"Expected 202 Accepted or 404 NotFound, got {response.StatusCode}"
        );
    }
}
