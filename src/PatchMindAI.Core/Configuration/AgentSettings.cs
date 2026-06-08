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

    public bool EnableTokenBudgeting { get; set; } = true;

    public int MaxRetrievedChunks { get; set; } = 5;

    public int MaxChunkChars { get; set; } = 800;

    public int SynthesisMaxOutputTokens { get; set; } = 1200;

    public int ParserMaxOutputTokens { get; set; } = 200;

    public int TokenWarningThreshold { get; set; } = 4000;

    public int IntentCacheTtlMinutes { get; set; } = 10;

    public int RetrievalCacheTtlMinutes { get; set; } = 5;

    public int SqlFactsCacheTtlMinutes { get; set; } = 5;

    public int CacheTimeWindowMinutes { get; set; } = 15;

    public int SearchRetryCount { get; set; } = 2;

    public int SearchRetryBaseDelayMs { get; set; } = 250;

    public int SearchCircuitBreakerFailureThreshold { get; set; } = 5;

    public int SearchCircuitBreakerCooldownSeconds { get; set; } = 60;

    public int OpenAiCircuitBreakerFailureThreshold { get; set; } = 5;

    public int OpenAiCircuitBreakerCooldownSeconds { get; set; } = 60;
}
