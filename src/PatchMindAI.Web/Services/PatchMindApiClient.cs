using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using PatchMindAI.Web.Configuration;
using PatchMindAI.Web.Models.Analysis;

namespace PatchMindAI.Web.Services;

public sealed class PatchMindApiClient : IPatchMindApiClient
{
    private readonly HttpClient _httpClient;

    public PatchMindApiClient(HttpClient httpClient, IOptions<ApiOptions> options)
    {
        _httpClient = httpClient;
        var baseUrl = options.Value.BaseUrl.TrimEnd('/');
        _httpClient.BaseAddress = new Uri(baseUrl + "/");
    }

    public async Task<AnalysisJobCreatedModel> CreateJobAsync(CreateJobRequestModel request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/analysis/jobs", request, cancellationToken);
        await EnsureSuccessAsync(response);

        var model = await response.Content.ReadFromJsonAsync<AnalysisJobCreatedModel>(cancellationToken: cancellationToken);
        return model ?? throw new InvalidOperationException("Empty create-job response from API.");
    }

    public async Task<PromptAnalysisCreatedModel> CreatePromptAsync(AnalyzePromptRequestModel request, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.PostAsJsonAsync("api/analysis/prompts", request, cancellationToken);
        await EnsureSuccessAsync(response);

        var model = await response.Content.ReadFromJsonAsync<PromptAnalysisCreatedModel>(cancellationToken: cancellationToken);
        return model ?? throw new InvalidOperationException("Empty create-prompt response from API.");
    }

    public async Task<AnalysisJobStatusModel> GetStatusAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"api/analysis/jobs/{jobId}/status", cancellationToken);
        await EnsureSuccessAsync(response);

        var model = await response.Content.ReadFromJsonAsync<AnalysisJobStatusModel>(cancellationToken: cancellationToken);
        return model ?? throw new InvalidOperationException("Empty status response from API.");
    }

    public async Task<JsonElement> GetResultAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync($"api/analysis/jobs/{jobId}/result", cancellationToken);

        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            using var pendingDoc = JsonDocument.Parse("{\"status\":\"Processing\"}");
            return pendingDoc.RootElement.Clone();
        }

        await EnsureSuccessAsync(response);

        var content = await response.Content.ReadAsStringAsync(cancellationToken);
        using var jsonDoc = JsonDocument.Parse(content);
        return jsonDoc.RootElement.Clone();
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        var body = await response.Content.ReadAsStringAsync();
        throw new HttpRequestException($"PatchMind API returned {(int)response.StatusCode}: {body}");
    }
}
