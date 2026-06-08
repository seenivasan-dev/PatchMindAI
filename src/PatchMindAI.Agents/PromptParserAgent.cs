using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Core.Models;

namespace PatchMindAI.Agents;

/// <summary>
/// Uses LLM to parse and classify user queries into structured intents.
/// </summary>
public sealed class PromptParserAgent : IPromptParserAgent
{
    private readonly IChatCompletionService _chatService;
    private readonly IMemoryCache _cache;
    private readonly AgentSettings _settings;
    private readonly ILogger<PromptParserAgent> _logger;

    private const string SystemPrompt = """
        You are an intent classification system for a vulnerability management platform.
        Analyze the user's query and classify it into one of these intents:

        1. CveSearch: User wants details about a specific CVE or to search vulnerabilities
           Examples: "What is CVE-2021-44228?", "Tell me about Log4Shell", "Heartbleed vulnerability"

        2. PriorityReport: User wants a ranked list of vulnerabilities to patch
           Examples: "What should I patch first?", "Top 10 critical vulnerabilities", "Most urgent patches"

        3. WeeklySummary: User wants a summary or trend analysis
           Examples: "Weekly report", "Show me this week's summary", "Vulnerability trends"

        4. AssetInventory: User wants asset information
           Examples: "Show me all assets", "Which systems are affected?", "List servers"

        Respond ONLY with valid JSON matching this schema:
        {
          "intent": "CveSearch|PriorityReport|WeeklySummary|AssetInventory|Unknown",
          "extractedCveId": "CVE-2021-44228 or null",
          "extractedKeywords": "relevant keywords or null",
          "topN": 10,
          "confidence": 0.95
        }
        """;

    public PromptParserAgent(
        IChatCompletionService chatService,
        IMemoryCache cache,
        IOptions<AgentSettings> options,
        ILogger<PromptParserAgent> logger)
    {
        _chatService = chatService;
        _cache = cache;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<ParsedIntent> ParseAsync(string userQuery, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userQuery))
        {
            return new ParsedIntent
            {
                Intent = QueryIntent.Unknown,
                OriginalQuery = userQuery,
                Confidence = 0.0
            };
        }

        var cveId = ExtractCveId(userQuery);
        if (cveId is not null)
        {
            return new ParsedIntent
            {
                Intent = QueryIntent.CveSearch,
                OriginalQuery = userQuery,
                ExtractedCveId = cveId,
                Confidence = 1.0
            };
        }

        var normalizedQuery = Normalize(userQuery);
        var cacheBucket = BuildWindowBucket(_settings.CacheTimeWindowMinutes);
        var cacheKey = $"intent:{normalizedQuery}:{cacheBucket}";

        if (_settings.IntentCacheTtlMinutes > 0
            && _cache.TryGetValue(cacheKey, out ParsedIntent? cached)
            && cached is not null)
        {
            _logger.LogDebug("Intent cache hit for query key {CacheKey}", cacheKey);
            return cached;
        }
        
        try
        {
            var chatHistory = new ChatHistory(SystemPrompt);
            chatHistory.AddUserMessage(userQuery);

            var executionSettings = new OpenAIPromptExecutionSettings
            {
                Temperature = 0,
                TopP = 1,
                MaxTokens = Math.Max(50, _settings.ParserMaxOutputTokens)
            };

            var responses = await _chatService.GetChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                kernel: null,
                cancellationToken: cancellationToken);

            var jsonContent = responses.FirstOrDefault()?.Content ?? "{}";
            
            // Parse LLM response
            var parsed = JsonSerializer.Deserialize<IntentResponse>(jsonContent, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (parsed is null)
            {
                _logger.LogWarning("Failed to parse LLM response for query: {Query}", userQuery);
                return CreateFallbackIntent(userQuery, cveId);
            }

            var result = new ParsedIntent
            {
                Intent = ParseIntentEnum(parsed.Intent),
                OriginalQuery = userQuery,
                ExtractedCveId = cveId ?? parsed.ExtractedCveId,
                ExtractedKeywords = parsed.ExtractedKeywords,
                TopN = parsed.TopN ?? 10,
                Confidence = parsed.Confidence
            };

            if (_settings.IntentCacheTtlMinutes > 0)
            {
                _cache.Set(cacheKey, result, TimeSpan.FromMinutes(_settings.IntentCacheTtlMinutes));
            }

            var promptTokens = EstimateTokens(userQuery) + EstimateTokens(SystemPrompt);
            var outputTokens = EstimateTokens(jsonContent);
            var totalTokens = promptTokens + outputTokens;

            _logger.LogInformation(
                "TokenBudget parser: prompt={PromptTokens} output={OutputTokens} total={TotalTokens}",
                promptTokens,
                outputTokens,
                totalTokens);

            if (_settings.EnableTokenBudgeting && totalTokens > _settings.TokenWarningThreshold)
            {
                _logger.LogWarning(
                    "TokenBudget threshold exceeded in parser stage: total={TotalTokens}, threshold={Threshold}",
                    totalTokens,
                    _settings.TokenWarningThreshold);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing user query: {Query}", userQuery);
            return CreateFallbackIntent(userQuery, cveId);
        }
    }

    private static string? ExtractCveId(string query)
    {
        var match = Regex.Match(query, @"CVE-\d{4}-\d{4,7}", RegexOptions.IgnoreCase);
        return match.Success ? match.Value.ToUpperInvariant() : null;
    }

    private static QueryIntent ParseIntentEnum(string? intentString)
    {
        return intentString?.ToLowerInvariant() switch
        {
            "cvesearch" => QueryIntent.CveSearch,
            "priorityreport" => QueryIntent.PriorityReport,
            "weeklysummary" => QueryIntent.WeeklySummary,
            "assetinventory" => QueryIntent.AssetInventory,
            _ => QueryIntent.Unknown
        };
    }

    private ParsedIntent CreateFallbackIntent(string userQuery, string? cveId)
    {
        // Use heuristics for fallback classification
        var queryLower = userQuery.ToLowerInvariant();

        if (cveId is not null || queryLower.Contains("cve") || queryLower.Contains("vulnerability"))
        {
            return new ParsedIntent
            {
                Intent = QueryIntent.CveSearch,
                OriginalQuery = userQuery,
                ExtractedCveId = cveId,
                Confidence = 0.6
            };
        }

        if (queryLower.Contains("priority") || queryLower.Contains("urgent") || 
            queryLower.Contains("critical") || queryLower.Contains("top"))
        {
            return new ParsedIntent
            {
                Intent = QueryIntent.PriorityReport,
                OriginalQuery = userQuery,
                Confidence = 0.6
            };
        }

        if (queryLower.Contains("week") || queryLower.Contains("summary") || 
            queryLower.Contains("report") || queryLower.Contains("trend"))
        {
            return new ParsedIntent
            {
                Intent = QueryIntent.WeeklySummary,
                OriginalQuery = userQuery,
                Confidence = 0.6
            };
        }

        return new ParsedIntent
        {
            Intent = QueryIntent.Unknown,
            OriginalQuery = userQuery,
            Confidence = 0.3
        };
    }

    private static int EstimateTokens(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return Math.Max(1, text.Length / 4);
    }

    private static string Normalize(string value)
    {
        return string.Join(' ', value
            .Trim()
            .ToLowerInvariant()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static long BuildWindowBucket(int windowMinutes)
    {
        var safeWindowMinutes = Math.Max(1, windowMinutes);
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (safeWindowMinutes * 60L);
    }

    private sealed class IntentResponse
    {
        public string? Intent { get; set; }
        public string? ExtractedCveId { get; set; }
        public string? ExtractedKeywords { get; set; }
        public int? TopN { get; set; }
        public double Confidence { get; set; }
    }
}
