using System.Text.Json;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Interfaces;
using StackExchange.Redis;

namespace PatchMindAI.Infrastructure.Caching;

public sealed class RedisAnalysisCache : IAnalysisCache
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IConnectionMultiplexer _multiplexer;
    private readonly RedisOptions _options;

    public RedisAnalysisCache(IConnectionMultiplexer multiplexer, RedisOptions options)
    {
        _multiplexer = multiplexer;
        _options = options;
    }

    public async Task SetJobAsync(AnalysisJob job, CancellationToken cancellationToken = default)
    {
        var key = BuildJobKey(job.Id);
        var value = JsonSerializer.Serialize(job, JsonOptions);
        var db = _multiplexer.GetDatabase();
        await db.StringSetAsync(key, value, TimeSpan.FromMinutes(_options.JobStatusTtlMinutes));
    }

    public async Task<AnalysisJob?> GetJobAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var db = _multiplexer.GetDatabase();
        var value = await db.StringGetAsync(BuildJobKey(jobId));
        return value.HasValue ? JsonSerializer.Deserialize<AnalysisJob>(value!, JsonOptions) : null;
    }

    public async Task SetResultAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        var key = BuildResultKey(result.JobId);
        var value = JsonSerializer.Serialize(result, JsonOptions);
        var db = _multiplexer.GetDatabase();
        await db.StringSetAsync(key, value, TimeSpan.FromMinutes(_options.ResultTtlMinutes));
    }

    public async Task<AnalysisResult?> GetResultAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        var db = _multiplexer.GetDatabase();
        var value = await db.StringGetAsync(BuildResultKey(jobId));
        return value.HasValue ? JsonSerializer.Deserialize<AnalysisResult>(value!, JsonOptions) : null;
    }

    private string BuildJobKey(Guid jobId) => $"{_options.KeyPrefix}:job:{jobId}";

    private string BuildResultKey(Guid jobId) => $"{_options.KeyPrefix}:result:{jobId}";
}
