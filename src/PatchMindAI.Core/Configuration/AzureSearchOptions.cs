namespace PatchMindAI.Core.Configuration;

public sealed class AzureSearchOptions
{
    public const string SectionName = "AzureSearch";

    public string Endpoint { get; set; } = string.Empty;

    public string IndexName { get; set; } = string.Empty;

    public string? ApiKey { get; set; }

    public bool UseManagedIdentity { get; set; } = true;

    public string SourceIdField { get; set; } = "id";

    public string ContentField { get; set; } = "content";

    public string? TitleField { get; set; }

    public bool EnableVectorSearch { get; set; } = true;

    public string VectorField { get; set; } = "contentVector";

    public int VectorDimensions { get; set; } = 1536;

    public string VectorProfileName { get; set; } = "cve-vector-profile";

    public string VectorAlgorithmName { get; set; } = "cve-hnsw";

    public string VectorizerName { get; set; } = "cve-openai-vectorizer";

    public string? AzureOpenAIEndpoint { get; set; }

    public string? AzureOpenAIEmbeddingDeployment { get; set; }

    public string? AzureOpenAIEmbeddingModelName { get; set; }

    public string? AzureOpenAIApiKey { get; set; }

    public bool BackfillVectorsOnStartup { get; set; }

    public bool FailStartupOnSeedError { get; set; }

    public int VectorBackfillBatchSize { get; set; } = 32;

    public double VectorCoverageReadinessThreshold { get; set; } = 0.9;

    public int VectorCoverageSampleSize { get; set; } = 1000;
}