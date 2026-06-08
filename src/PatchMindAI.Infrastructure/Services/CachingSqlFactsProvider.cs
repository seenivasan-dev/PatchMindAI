using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Core.Models;

namespace PatchMindAI.Infrastructure.Services;

public sealed class CachingSqlFactsProvider : ISqlFactsProvider
{
    private readonly ISqlFactsProvider _inner;
    private readonly IMemoryCache _cache;
    private readonly AgentSettings _settings;
    private readonly ILogger<CachingSqlFactsProvider> _logger;

    public CachingSqlFactsProvider(
        ISqlFactsProvider inner,
        IMemoryCache cache,
        IOptions<AgentSettings> options,
        ILogger<CachingSqlFactsProvider> logger)
    {
        _inner = inner;
        _cache = cache;
        _settings = options.Value;
        _logger = logger;
    }

    public async Task<SqlFactSnapshot> GetFactsForCveAsync(string cveId, int topAssets = 10, CancellationToken cancellationToken = default)
    {
        var boundedTopAssets = Math.Max(1, topAssets);

        if (_settings.SqlFactsCacheTtlMinutes <= 0)
        {
            return await _inner.GetFactsForCveAsync(cveId, boundedTopAssets, cancellationToken);
        }

        var bucket = BuildWindowBucket(_settings.CacheTimeWindowMinutes);
        var cacheKey = $"sqlfacts:{cveId.ToUpperInvariant()}:{boundedTopAssets}:{bucket}";

        if (_cache.TryGetValue(cacheKey, out SqlFactSnapshot? cached) && cached is not null)
        {
            _logger.LogDebug("SQL facts cache hit for {CveId}", cveId);
            return cached;
        }

        var facts = await _inner.GetFactsForCveAsync(cveId, boundedTopAssets, cancellationToken);
        _cache.Set(cacheKey, facts, TimeSpan.FromMinutes(_settings.SqlFactsCacheTtlMinutes));
        return facts;
    }

    private static long BuildWindowBucket(int windowMinutes)
    {
        var safeWindowMinutes = Math.Max(1, windowMinutes);
        return DateTimeOffset.UtcNow.ToUnixTimeSeconds() / (safeWindowMinutes * 60L);
    }
}
