namespace PatchMindAI.Core.Configuration;

public sealed class AzureOpenAIOptions
{
    public const string SectionName = "AzureOpenAI";

    public string Endpoint { get; set; } = string.Empty;

    public string DeploymentName { get; set; } = string.Empty;

    public string? ParserDeploymentName { get; set; }

    public string Model { get; set; } = "gpt-4o";

    public string? ParserModel { get; set; }

    public string? ApiKey { get; set; }

    public string? ParserApiKey { get; set; }

    public bool UseManagedIdentity { get; set; } = true;

    public string? ApiVersion { get; set; }
}
