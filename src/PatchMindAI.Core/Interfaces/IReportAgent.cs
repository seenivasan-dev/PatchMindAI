using PatchMindAI.Core.Models;

namespace PatchMindAI.Core.Interfaces;

/// <summary>
/// Generates weekly reports and trend analysis for vulnerability management.
/// </summary>
public interface IReportAgent
{
    /// <summary>
    /// Generates a weekly vulnerability management report.
    /// </summary>
    Task<WeeklyReport> GenerateWeeklyReportAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Generates an executive summary from the weekly report using LLM.
    /// </summary>
    Task<string> GenerateExecutiveSummaryAsync(
        WeeklyReport report,
        CancellationToken cancellationToken = default);
}
