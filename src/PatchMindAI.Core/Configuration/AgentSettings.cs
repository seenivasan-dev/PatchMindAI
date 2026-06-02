namespace PatchMindAI.Core.Configuration;

public sealed class AgentSettings
{
    public const string SectionName = "AgentSettings";

    public int MaxTurns { get; set; } = 8;

    public bool EnableReflection { get; set; } = true;

    public bool EnableRag { get; set; }

    public int ToolCallDepthLimit { get; set; } = 4;

    public bool RequireAzurePipeline { get; set; }
    
    /// <summary>
    /// Enable multi-agent architecture with intent routing.
    /// </summary>
    public bool EnableMultiAgentArchitecture { get; set; }
}
