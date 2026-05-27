namespace PatchMindAI.Core.Contracts;

public sealed class AnalysisRequestMessage
{
    public Guid JobId { get; set; }

    public string CveId { get; set; } = string.Empty;

    public string UserQuery { get; set; } = string.Empty;
}
