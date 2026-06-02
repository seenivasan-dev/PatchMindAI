using System.Text.Json;
using Azure.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Agents;

public sealed class AzureOpenAiAnalysisOrchestrator : IAnalysisOrchestrator
{
    private readonly INvdClient _nvdClient;
    private readonly IKnowledgeRetriever _knowledgeRetriever;
    private readonly AzureOpenAIChatCompletionService _chatService;
    private readonly Kernel _kernel;

    public AzureOpenAiAnalysisOrchestrator(
        INvdClient nvdClient,
        IKnowledgeRetriever knowledgeRetriever,
        IOptions<AzureOpenAIOptions> options,
        ILoggerFactory loggerFactory)
    {
        _nvdClient = nvdClient;
        _knowledgeRetriever = knowledgeRetriever;

        var azureOptions = options.Value;
        if (string.IsNullOrWhiteSpace(azureOptions.Endpoint) || string.IsNullOrWhiteSpace(azureOptions.DeploymentName))
        {
            throw new InvalidOperationException("AzureOpenAI endpoint and deployment name must be configured for the Azure orchestrator.");
        }

        var apiVersion = string.IsNullOrWhiteSpace(azureOptions.ApiVersion)
            ? null
            : azureOptions.ApiVersion;

        _kernel = Kernel.CreateBuilder().Build();

        if (!string.IsNullOrWhiteSpace(azureOptions.ApiKey))
        {
            _chatService = new AzureOpenAIChatCompletionService(
                azureOptions.DeploymentName,
                azureOptions.Endpoint,
                azureOptions.ApiKey,
                azureOptions.Model,
                httpClient: null,
                loggerFactory,
                apiVersion);
            return;
        }

        if (!azureOptions.UseManagedIdentity)
        {
            throw new InvalidOperationException("AzureOpenAI requires either ApiKey or UseManagedIdentity=true when configured.");
        }

        _chatService = new AzureOpenAIChatCompletionService(
            azureOptions.DeploymentName,
            azureOptions.Endpoint,
            CreateTokenCredential(),
            azureOptions.Model,
            httpClient: null,
            loggerFactory,
            apiVersion);
    }

    public async Task<AnalysisResult> RunAsync(AnalysisJob job, CancellationToken cancellationToken = default)
    {
        var cve = await _nvdClient.GetCveByIdAsync(job.CveId, cancellationToken);
        if (cve is null)
        {
            throw new InvalidOperationException($"CVE '{job.CveId}' was not found.");
        }

        var retrievalQuery = string.IsNullOrWhiteSpace(job.UserQuery) ? job.CveId : job.UserQuery;
        var retrievedChunks = await _knowledgeRetriever.RetrieveAsync(retrievalQuery, 5, cancellationToken);
        if (retrievedChunks.Count == 0 && !retrievalQuery.Equals(job.CveId, StringComparison.OrdinalIgnoreCase))
        {
            retrievedChunks = await _knowledgeRetriever.RetrieveAsync(job.CveId, 5, cancellationToken);
        }
        var prompt = BuildPrompt(job, cve, retrievedChunks);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0,
            TopP = 1,
            MaxTokens = 1200
        };

        var textResponses = await _chatService.GetTextContentsAsync(prompt, executionSettings, _kernel, cancellationToken);
        var assistantText = textResponses.FirstOrDefault()?.Text ?? string.Empty;
        var parsed = ParseJsonPayload(assistantText);

        return new AnalysisResult
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            RiskScore = ReadDouble(parsed, "riskScore", fallback: Math.Max(cve.BaseScore, cve.Severity is SeverityLevel.Critical ? 9.5 : cve.BaseScore)),
            RiskJustification = ReadString(parsed, "riskJustification", fallback: $"Risk is derived from base score {cve.BaseScore} and severity {cve.Severity}.")!,
            ImpactSummary = ReadString(parsed, "impactSummary", fallback: $"{cve.Id} impacts {cve.AffectedProducts.Length} product groups. Immediate review is recommended.")!,
            AffectedAssetsJson = ReadRawJson(parsed, "affectedAssetsJson", fallback: JsonSerializer.Serialize(cve.AffectedProducts)),
            RemediationStepsJson = ReadRawJson(parsed, "remediationStepsJson", fallback: JsonSerializer.Serialize(new[]
            {
                new { priority = "Critical", action = $"Patch affected products listed for {cve.Id}." },
                new { priority = "High", action = "Apply temporary mitigations and monitor exploit attempts." }
            })),
            RawAgentOutputJson = JsonSerializer.Serialize(new
            {
                planner = "azure-openai",
                model = "semantic-kernel",
                source = "AzureOpenAiAnalysisOrchestrator",
                cveId = cve.Id,
                assistantText,
                retrievedChunks = retrievedChunks.Select(chunk => new
                {
                    sourceId = chunk.SourceId,
                    score = chunk.Score,
                    text = chunk.Text
                })
            }),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private static string BuildPrompt(AnalysisJob job, Core.Domain.Cve cve, IReadOnlyList<RetrievedChunk> retrievedChunks)
    {
        var citations = retrievedChunks.Count == 0
            ? "No supporting chunks were retrieved."
            : string.Join("\n", retrievedChunks.Select((chunk, index) => $"[{index + 1}] {chunk.SourceId} | score={chunk.Score:0.000} | {chunk.Text}"));

        return $@"
You are a security analyst. Return only valid JSON with this exact shape:
{{
  ""riskScore"": number,
  ""riskJustification"": string,
  ""impactSummary"": string,
  ""affectedAssetsJson"": [string],
  ""remediationStepsJson"": [{{ ""priority"": string, ""action"": string }}]
}}

Use the CVE record and retrieved citations to produce a concise, evidence-based assessment.
Do not wrap the JSON in markdown fences.

CVE:
- Id: {cve.Id}
- Description: {cve.Description}
- Severity: {cve.Severity}
- BaseScore: {cve.BaseScore}
- AffectedProducts: {string.Join(", ", cve.AffectedProducts)}

User question:
{job.UserQuery}

Retrieved citations:
{citations}
";
    }

    private static JsonElement ParseJsonPayload(string content)
    {
        var trimmed = content.Trim();
        var start = trimmed.IndexOf('{');
        var end = trimmed.LastIndexOf('}');

        if (start < 0 || end < start)
        {
            throw new InvalidOperationException("Azure OpenAI did not return valid JSON.");
        }

        var json = trimmed[start..(end + 1)];
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static string? ReadString(JsonElement element, string propertyName, string? fallback)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.ValueKind == JsonValueKind.String)
        {
            return prop.GetString();
        }

        return fallback;
    }

    private static double ReadDouble(JsonElement element, string propertyName, double fallback)
    {
        if (element.TryGetProperty(propertyName, out var prop) && prop.TryGetDouble(out var value))
        {
            return value;
        }

        return fallback;
    }

    private static string ReadRawJson(JsonElement element, string propertyName, string fallback)
    {
        return element.TryGetProperty(propertyName, out var prop) ? prop.GetRawText() : fallback;
    }

    private static Azure.Core.TokenCredential CreateTokenCredential()
    {
        return new ChainedTokenCredential(
            new AzureCliCredential(),
            new DefaultAzureCredential(new DefaultAzureCredentialOptions
            {
                ExcludeAzureCliCredential = true
            }));
    }
}