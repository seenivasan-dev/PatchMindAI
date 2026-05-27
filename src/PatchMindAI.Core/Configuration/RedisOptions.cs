namespace PatchMindAI.Core.Configuration;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    public string Provider { get; set; } = "InMemory";

    public string ConnectionString { get; set; } = "localhost:6379,abortConnect=false";

    public string KeyPrefix { get; set; } = "patchmindai";

    public int JobStatusTtlMinutes { get; set; } = 60;

    public int ResultTtlMinutes { get; set; } = 240;
}
