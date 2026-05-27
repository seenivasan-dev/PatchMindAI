namespace PatchMindAI.API.Contracts;

public sealed class AnalysisJobCreatedResponse
{
    public Guid JobId { get; set; }

    public string Status { get; set; } = string.Empty;
}
