using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Azure;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Core.Models;

namespace PatchMindAI.Infrastructure.Services;

public sealed class CachingKnowledgeRetriever : IKnowledgeRetriever
{
    private readonly IKnowledgeRetriever _inner;
    private readonly IMemoryCache _cache;
    private readonly AgentSettings _settings;
    private readonly ILogger<CachingKnowledgeRetriever> _logger;
    private readonly object _circuitLock = new();
    private int _consecutiveTransientFailures;
    private DateTimeOffset? _circuitOpenUntilUtc;

    public CachingKnowledgeRetriever(
        IKnowledgeRetriever inner,
        IMemoryCache cache,
        IOptions<AgentSettings> options,
        ILogger<CachingKnowledgeRetriever> logger)
    {
        _inner = inner;
        _cache = cache;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        var normalizedQuery = Normalize(query);
        var boundedTopK = Math.Max(1, Math.Min(topK, Math.Max(1, _settings.MaxRetrievedChunks)));

        if (IsCircuitOpen(out var openUntil))
        {
            _logger.LogWarning("Search retrieval circuit breaker is open until {OpenUntil}. Returning empty retrieval result.", openUntil);
            return Array.Empty<RetrievedChunk>();
        }

        if (_settings.RetrievalCacheTtlMinutes <= 0)
        {
            var uncached = await RetrieveWithRetryAsync(query, boundedTopK, cancellationToken);
            return ApplyBudget(uncached, boundedTopK);
        }

        var bucket = BuildWindowBucket(_settings.CacheTimeWindowMinutes);
        var cacheKey = $"retrieval:{normalizedQuery}:{boundedTopK}:{bucket}";

        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<RetrievedChunk>? cached) && cached is not null)
        {
            _logger.LogDebug("Retrieval cache hit for key {CacheKey}", cacheKey);
            return cached;
        }

        var fresh = await RetrieveWithRetryAsync(query, boundedTopK, cancellationToken);
        var budgeted = ApplyBudget(fresh, boundedTopK);
        _cache.Set(cacheKey, budgeted, TimeSpan.FromMinutes(_settings.RetrievalCacheTtlMinutes));

        return budgeted;
    }

    private async Task<IReadOnlyList<RetrievedChunk>> RetrieveWithRetryAsync(string query, int topK, CancellationToken cancellationToken)
    {
        var maxRetries = Math.Max(0, _settings.SearchRetryCount);

        for (var attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                var results = await _inner.RetrieveAsync(query, topK, cancellationToken);
                RecordSuccess();
                return results;
            }
            catch (Exception ex) when (IsTransientSearchError(ex) && attempt < maxRetries)
            {
                RecordFailure();

                var delay = TimeSpan.FromMilliseconds(Math.Max(50, _settings.SearchRetryBaseDelayMs) * Math.Pow(2, attempt));
                _logger.LogWarning(ex, "Transient search error. Retrying attempt {Attempt}/{MaxRetries} after {DelayMs}ms", attempt + 1, maxRetries, delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
            catch
            {
                RecordSuccess();
                throw;
            }
        }

        try
        {
            var finalResult = await _inner.RetrieveAsync(query, topK, cancellationToken);
            RecordSuccess();
            return finalResult;
        }
        catch (Exception ex) when (IsTransientSearchError(ex))
        {
            RecordFailure();
            _logger.LogWarning(ex, "Search retrieval failed after retries. Returning empty result to preserve availability.");
            return Array.Empty<RetrievedChunk>();
        }
    }

    private static bool IsTransientSearchError(Exception ex)
    {
        if (ex is RequestFailedException requestFailed)
        {
            return requestFailed.Status == 408
                   || requestFailed.Status == 429
                   || requestFailed.Status == 500
                   || requestFailed.Status == 502
                   || requestFailed.Status == 503
                   || requestFailed.Status == 504;
        }

        var exceptionMessage = ex.Message;
        if (exceptionMessage.Contains("HTTP 429", StringComparison.OrdinalIgnoreCase)
            || exceptionMessage.Contains("HTTP 503", StringComparison.OrdinalIgnoreCase)
            || exceptionMessage.Contains("HTTP 504", StringComparison.OrdinalIgnoreCase)
            || exceptionMessage.Contains("too_many_requests", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return ex.InnerException is not null && IsTransientSearchError(ex.InnerException);
    }

    private bool IsCircuitOpen(out DateTimeOffset? openUntil)
    {
        lock (_circuitLock)
        {
            openUntil = _circuitOpenUntilUtc;
            return _circuitOpenUntilUtc.HasValue && _circuitOpenUntilUtc.Value > DateTimeOffset.UtcNow;
        }
    }

    private void RecordFailure()
    {
        lock (_circuitLock)
        {
            _consecutiveTransientFailures++;
            var threshold = Math.Max(1, _settings.SearchCircuitBreakerFailureThreshold);

            if (_consecutiveTransientFailures >= threshold)
            {
                _circuitOpenUntilUtc = DateTimeOffset.UtcNow.AddSeconds(
                    Math.Max(5, _settings.SearchCircuitBreakerCooldownSeconds));
            }
        }
    }

    private void RecordSuccess()
    {
        lock (_circuitLock)
        {
            _consecutiveTransientFailures = 0;
            _circuitOpenUntilUtc = null;
        }
    }

    private IReadOnlyList<RetrievedChunk> ApplyBudget(IReadOnlyList<RetrievedChunk> chunks, int topK)
    {
        var maxChunkChars = Math.Max(100, _settings.MaxChunkChars);

        return chunks
            .Where(c => !string.IsNullOrWhiteSpace(c.Text))
            .GroupBy(c => $"{c.SourceId}|{c.Text.Trim()}", StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .OrderByDescending(c => c.Score)
            .Take(topK)
            .Select(c => new RetrievedChunk
            {
                SourceId = c.SourceId,
                Score = c.Score,
                Text = c.Text.Length <= maxChunkChars ? c.Text : c.Text[..maxChunkChars]
            })
            .ToArray();
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
}
