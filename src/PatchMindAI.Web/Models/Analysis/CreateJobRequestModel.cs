namespace PatchMindAI.Web.Models.Analysis;

public sealed class CreateJobRequestModel
{
    public string CveId { get; set; } = string.Empty;

    public string UserQuery { get; set; } = string.Empty;
}
