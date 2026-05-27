namespace PatchMindAI.Core.Configuration;

public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; set; } = string.Empty;

    public string DeploymentName { get; set; } = string.Empty;

    public string Model { get; set; } = "gpt-4o";

    public string? ApiKey { get; set; }

    public bool UseManagedIdentity { get; set; } = true;

    public string? ApiVersion { get; set; }
}
