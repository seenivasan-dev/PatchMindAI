namespace PatchMindAI.Web.Configuration;

public sealed class ApiOptions
{
    public const string SectionName = "PatchMindApi";

    public string BaseUrl { get; set; } = "http://localhost:5101";
}
