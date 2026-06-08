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
    private readonly ISqlFactsProvider _sqlFactsProvider;
    private readonly IDeterministicRiskScorer _riskScorer;
    private readonly AgentSettings _agentSettings;
    private readonly ILogger<AzureOpenAiAnalysisOrchestrator> _logger;
    private readonly AzureOpenAIChatCompletionService _chatService;
    private readonly Kernel _kernel;

    public AzureOpenAiAnalysisOrchestrator(
        INvdClient nvdClient,
        IKnowledgeRetriever knowledgeRetriever,
        ISqlFactsProvider sqlFactsProvider,
        IDeterministicRiskScorer riskScorer,
        IOptions<AgentSettings> agentSettings,
        IOptions<AzureOpenAIOptions> options,
        ILogger<AzureOpenAiAnalysisOrchestrator> logger,
        ILoggerFactory loggerFactory)
    {
        _nvdClient = nvdClient;
        _knowledgeRetriever = knowledgeRetriever;
        _sqlFactsProvider = sqlFactsProvider;
        _riskScorer = riskScorer;
        _agentSettings = agentSettings.Value;
        _logger = logger;

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
        var retrievalTopK = Math.Max(1, _agentSettings.MaxRetrievedChunks);
        var retrievedChunks = await _knowledgeRetriever.RetrieveAsync(retrievalQuery, retrievalTopK, cancellationToken);
        if (retrievedChunks.Count == 0 && !retrievalQuery.Equals(job.CveId, StringComparison.OrdinalIgnoreCase))
        {
            retrievedChunks = await _knowledgeRetriever.RetrieveAsync(job.CveId, retrievalTopK, cancellationToken);
        }

        var sqlFacts = await _sqlFactsProvider.GetFactsForCveAsync(job.CveId, 10, cancellationToken);
        var scoring = _riskScorer.Score(cve, sqlFacts);
        var prompt = BuildPrompt(job, cve, retrievedChunks, sqlFacts, scoring);

        var executionSettings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0,
            TopP = 1,
            MaxTokens = Math.Max(200, _agentSettings.SynthesisMaxOutputTokens)
        };

        var textResponses = await _chatService.GetTextContentsAsync(prompt, executionSettings, _kernel, cancellationToken);
        var assistantText = textResponses.FirstOrDefault()?.Text ?? string.Empty;

        LogTokenUsage(job, retrievalQuery, retrievedChunks, prompt, assistantText, sqlFacts);

        var parsed = ParseJsonPayload(assistantText);

        return new AnalysisResult
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            // Risk score remains deterministic and SQL-grounded for stable ordering.
            RiskScore = scoring.OverallScore,
            RiskJustification = ReadString(parsed, "riskJustification", fallback: scoring.Justification)!,
            ImpactSummary = ReadString(parsed, "impactSummary", fallback: $"{cve.Id} affects {sqlFacts.TotalVulnerableAssets} assets ({sqlFacts.InternetFacingVulnerableAssets} internet-facing).")!,
            AffectedAssetsJson = ReadRawJson(parsed, "affectedAssetsJson", fallback: JsonSerializer.Serialize(sqlFacts.RankedAssets)),
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
                sqlFacts,
                deterministicScore = scoring,
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

    private static string BuildPrompt(
        AnalysisJob job,
        Core.Domain.Cve cve,
        IReadOnlyList<RetrievedChunk> retrievedChunks,
        Core.Models.SqlFactSnapshot sqlFacts,
        Core.Models.RiskScoringResult scoring)
    {
        var citations = retrievedChunks.Count == 0
            ? "No supporting chunks were retrieved."
            : string.Join("\n", retrievedChunks.Select((chunk, index) => $"[{index + 1}] {chunk.SourceId} | score={chunk.Score:0.000} | {chunk.Text}"));

        var sqlFactsJson = JsonSerializer.Serialize(sqlFacts);
        var scoringJson = JsonSerializer.Serialize(scoring);

        return $@"
You are a security analyst. Return only valid JSON with this exact shape:
{{
    ""riskScore"": number,
  ""riskJustification"": string,
  ""impactSummary"": string,
    ""affectedAssetsJson"": [{{ ""assetId"": string, ""hostname"": string, ""criticality"": string, ""isInternetFacing"": bool, ""daysOpen"": number, ""priorityScore"": number }}],
  ""remediationStepsJson"": [{{ ""priority"": string, ""action"": string }}]
}}

Use the CVE record, retrieved citations, SQL facts, and deterministic scoring result to produce a concise, evidence-based assessment.
Do not invent counts; use SQL facts exactly. Include top ranked assets from SQL facts in affectedAssetsJson.
For riskScore, echo the deterministic score value exactly.
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

SQL facts (authoritative):
{sqlFactsJson}

Deterministic score (authoritative):
{scoringJson}
";
    }

    private void LogTokenUsage(
        AnalysisJob job,
        string retrievalQuery,
        IReadOnlyList<RetrievedChunk> retrievedChunks,
        string prompt,
        string assistantText,
        Core.Models.SqlFactSnapshot sqlFacts)
    {
        var retrievalTokens = EstimateTokens(retrievalQuery) + EstimateTokens(string.Join(' ', retrievedChunks.Select(c => c.Text)));
        var sqlTokens = EstimateTokens(JsonSerializer.Serialize(sqlFacts));
        var synthesisPromptTokens = EstimateTokens(prompt);
        var synthesisOutputTokens = EstimateTokens(assistantText);
        var total = retrievalTokens + sqlTokens + synthesisPromptTokens + synthesisOutputTokens;

        _logger.LogInformation(
            "TokenBudget job={JobId}: retrieval={RetrievalTokens} sql={SqlTokens} synthesisPrompt={SynthesisPromptTokens} synthesisOutput={SynthesisOutputTokens} total={TotalTokens}",
            job.Id,
            retrievalTokens,
            sqlTokens,
            synthesisPromptTokens,
            synthesisOutputTokens,
            total);

        if (_agentSettings.EnableTokenBudgeting && total > _agentSettings.TokenWarningThreshold)
        {
            _logger.LogWarning(
                "TokenBudget threshold exceeded for job {JobId}: total={TotalTokens}, threshold={Threshold}",
                job.Id,
                total,
                _agentSettings.TokenWarningThreshold);
        }
    }

    private static int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return Math.Max(1, text.Length / 4);
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