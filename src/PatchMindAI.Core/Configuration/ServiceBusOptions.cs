namespace PatchMindAI.Core.Configuration;

public sealed class ServiceBusOptions
{
    public const string SectionName = "ServiceBus";

    public string Provider { get; set; } = "InMemory";

    public string FullyQualifiedNamespace { get; set; } = string.Empty;

    public string ConnectionString { get; set; } = string.Empty;

    public string QueueName { get; set; } = "cve-analysis-jobs";
}
