namespace PatchMindAI.Web.Models.Analysis;

public sealed class AnalysisJobCreatedModel
{
    public Guid JobId { get; set; }

    public string Status { get; set; } = string.Empty;
}
