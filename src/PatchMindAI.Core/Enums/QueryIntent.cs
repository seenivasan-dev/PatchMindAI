namespace PatchMindAI.Core.Enums;

/// <summary>
/// Represents the classified intent of a user's query.
/// </summary>
public enum QueryIntent
{
    /// <summary>
    /// User wants details about a specific CVE or vulnerability search.
    /// </summary>
    CveSearch,

    /// <summary>
    /// User wants a prioritized list of vulnerabilities to patch.
    /// </summary>
    PriorityReport,

    /// <summary>
    /// User wants a weekly summary or trend analysis.
    /// </summary>
    WeeklySummary,

    /// <summary>
    /// User wants asset inventory information.
    /// </summary>
    AssetInventory,

    /// <summary>
    /// Unable to classify the intent.
    /// </summary>
    Unknown
}
