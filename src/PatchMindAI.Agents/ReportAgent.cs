using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Core.Models;
using PatchMindAI.Infrastructure.Data;

namespace PatchMindAI.Agents;

/// <summary>
/// Generates weekly reports and trend analysis for vulnerability management.
/// </summary>
public sealed class ReportAgent : IReportAgent
{
    private readonly PatchMindDbContext _context;
    private readonly IPrioritizationAgent _prioritizationAgent;
    private readonly IChatCompletionService _chatService;
    private readonly ILogger<ReportAgent> _logger;

    public ReportAgent(
        PatchMindDbContext context,
        IPrioritizationAgent prioritizationAgent,
        IChatCompletionService chatService,
        ILogger<ReportAgent> logger)
    {
        _context = context;
        _prioritizationAgent = prioritizationAgent;
        _chatService = chatService;
        _logger = logger;
    }

    public async Task<WeeklyReport> GenerateWeeklyReportAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        CancellationToken cancellationToken = default)
    {
        var reportEnd = endDate ?? DateTime.UtcNow;
        var reportStart = startDate ?? reportEnd.AddDays(-7);

        _logger.LogInformation("Generating weekly report from {StartDate} to {EndDate}", reportStart, reportEnd);

        // Gather statistics
        var statistics = await GatherStatisticsAsync(reportStart, reportEnd, cancellationToken);
        
        // Calculate trends (comparing to previous period)
        var trends = await CalculateTrendsAsync(reportStart, reportEnd, cancellationToken);
        
        // Get top vulnerabilities
        var topVulnerabilities = await _prioritizationAgent.GetPrioritizedVulnerabilitiesAsync(10, cancellationToken);
        
        // Extract key findings
        var keyFindings = ExtractKeyFindings(statistics, topVulnerabilities);

        var report = new WeeklyReport
        {
            ReportStartDate = reportStart,
            ReportEndDate = reportEnd,
            GeneratedAtUtc = DateTime.UtcNow,
            Statistics = statistics,
            Trends = trends,
            TopVulnerabilities = topVulnerabilities,
            KeyFindings = keyFindings,
            ExecutiveSummary = string.Empty // Will be generated separately
        };

        // Generate executive summary using LLM
        var executiveSummary = await GenerateExecutiveSummaryAsync(report, cancellationToken);
        return new WeeklyReport
        {
            ReportStartDate = report.ReportStartDate,
            ReportEndDate = report.ReportEndDate,
            GeneratedAtUtc = report.GeneratedAtUtc,
            Statistics = report.Statistics,
            Trends = report.Trends,
            TopVulnerabilities = report.TopVulnerabilities,
            KeyFindings = report.KeyFindings,
            ExecutiveSummary = executiveSummary
        };

        return report;
    }

    public async Task<string> GenerateExecutiveSummaryAsync(
        WeeklyReport report,
        CancellationToken cancellationToken = default)
    {
        var prompt = $"""
            Generate a concise executive summary (3-4 sentences) for this vulnerability management report:

            Period: {report.ReportStartDate:yyyy-MM-dd} to {report.ReportEndDate:yyyy-MM-dd}
            
            Statistics:
            - Total Vulnerabilities: {report.Statistics.TotalVulnerabilities}
            - Critical: {report.Statistics.CriticalVulnerabilities}
            - High: {report.Statistics.HighVulnerabilities}
            - Patched This Week: {report.Statistics.PatchedThisWeek}
            - Newly Discovered: {report.Statistics.NewlyDiscovered}
            - Overdue Patches: {report.Statistics.OverduePatches}
            - Internet-Facing Affected: {report.Statistics.InternetFacingAffected}

            Key Findings:
            {string.Join("\n", report.KeyFindings)}

            Top Vulnerability: {report.TopVulnerabilities.FirstOrDefault()?.Cve.Id ?? "None"}

            Focus on actionable insights and business impact. Be concise and professional.
            """;

        try
        {
            var chatHistory = new ChatHistory();
            chatHistory.AddUserMessage(prompt);

            var response = await _chatService.GetChatMessageContentAsync(
                chatHistory,
                cancellationToken: cancellationToken);

            return response.Content ?? "Executive summary generation failed.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate executive summary");
            return "Unable to generate executive summary at this time.";
        }
    }

    private async Task<WeeklyStatistics> GatherStatisticsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var allPatchStatuses = await _context.PatchStatuses
            .Include(ps => ps.Cve)
            .Include(ps => ps.Asset)
            .ToListAsync(cancellationToken);

        var totalVulnerabilities = allPatchStatuses.Count(ps => ps.Status == PatchingStatus.Vulnerable);
        
        var criticalVulnerabilities = allPatchStatuses.Count(ps =>
            ps.Status == PatchingStatus.Vulnerable &&
            ps.Cve != null &&
            ps.Cve.Severity == SeverityLevel.Critical);
        
        var highVulnerabilities = allPatchStatuses.Count(ps =>
            ps.Status == PatchingStatus.Vulnerable &&
            ps.Cve != null &&
            ps.Cve.Severity == SeverityLevel.High);

        var patchedThisWeek = allPatchStatuses.Count(ps =>
            ps.Status == PatchingStatus.Patched &&
            ps.PatchedAtUtc.HasValue &&
            ps.PatchedAtUtc.Value >= startDate &&
            ps.PatchedAtUtc.Value <= endDate);

        var newlyDiscovered = allPatchStatuses.Count(ps =>
            ps.DetectedAtUtc >= startDate &&
            ps.DetectedAtUtc <= endDate);

        var overduePatches = allPatchStatuses.Count(ps =>
            ps.Status == PatchingStatus.Vulnerable &&
            ps.TargetPatchDate.HasValue &&
            ps.TargetPatchDate.Value < DateTime.UtcNow);

        var patchedItems = allPatchStatuses.Where(ps =>
            ps.Status == PatchingStatus.Patched &&
            ps.PatchedAtUtc.HasValue &&
            ps.DetectedAtUtc < ps.PatchedAtUtc.Value);

        var avgPatchTime = patchedItems.Any()
            ? patchedItems.Average(ps => (ps.PatchedAtUtc!.Value - ps.DetectedAtUtc).TotalDays)
            : 0.0;

        var affectedAssets = allPatchStatuses
            .Where(ps => ps.Status == PatchingStatus.Vulnerable && ps.Asset != null)
            .Select(ps => ps.AssetId)
            .Distinct()
            .Count();

        var internetFacingAffected = allPatchStatuses
            .Where(ps => ps.Status == PatchingStatus.Vulnerable && 
                        ps.Asset != null && 
                        ps.Asset.IsInternetFacing)
            .Select(ps => ps.AssetId)
            .Distinct()
            .Count();

        return new WeeklyStatistics
        {
            TotalVulnerabilities = totalVulnerabilities,
            CriticalVulnerabilities = criticalVulnerabilities,
            HighVulnerabilities = highVulnerabilities,
            PatchedThisWeek = patchedThisWeek,
            NewlyDiscovered = newlyDiscovered,
            OverduePatches = overduePatches,
            AveragePatchTime = avgPatchTime,
            TotalAffectedAssets = affectedAssets,
            InternetFacingAffected = internetFacingAffected
        };
    }

    private async Task<List<TrendItem>> CalculateTrendsAsync(
        DateTime startDate,
        DateTime endDate,
        CancellationToken cancellationToken)
    {
        var trends = new List<TrendItem>();

        // Compare current period to previous period
        var previousStart = startDate.AddDays(-7);
        var previousEnd = startDate;

        var currentStats = await GatherStatisticsAsync(startDate, endDate, cancellationToken);
        var previousStats = await GatherStatisticsAsync(previousStart, previousEnd, cancellationToken);

        // Calculate vulnerability trend
        var vulnChange = previousStats.TotalVulnerabilities > 0
            ? ((currentStats.TotalVulnerabilities - previousStats.TotalVulnerabilities) / (double)previousStats.TotalVulnerabilities) * 100
            : 0;

        trends.Add(new TrendItem
        {
            Category = "Total Vulnerabilities",
            Direction = vulnChange > 5 ? "increasing" : vulnChange < -5 ? "decreasing" : "stable",
            ChangePercentage = vulnChange,
            Description = $"Changed by {vulnChange:F1}% compared to previous week"
        });

        // Calculate patching rate trend
        var patchChange = previousStats.PatchedThisWeek > 0
            ? ((currentStats.PatchedThisWeek - previousStats.PatchedThisWeek) / (double)previousStats.PatchedThisWeek) * 100
            : 0;

        trends.Add(new TrendItem
        {
            Category = "Patching Activity",
            Direction = patchChange > 5 ? "increasing" : patchChange < -5 ? "decreasing" : "stable",
            ChangePercentage = patchChange,
            Description = $"Patching rate changed by {patchChange:F1}% compared to previous week"
        });

        return trends;
    }

    private List<string> ExtractKeyFindings(
        WeeklyStatistics statistics,
        List<PrioritizedVulnerability> topVulnerabilities)
    {
        var findings = new List<string>();

        if (statistics.CriticalVulnerabilities > 0)
        {
            findings.Add($"{statistics.CriticalVulnerabilities} critical vulnerabilities require immediate attention");
        }

        if (statistics.OverduePatches > 0)
        {
            findings.Add($"{statistics.OverduePatches} patches are past their target date");
        }

        if (statistics.InternetFacingAffected > 0)
        {
            findings.Add($"{statistics.InternetFacingAffected} internet-facing assets are vulnerable");
        }

        if (statistics.PatchedThisWeek > 0)
        {
            findings.Add($"{statistics.PatchedThisWeek} vulnerabilities were successfully patched this week");
        }

        if (statistics.AveragePatchTime > 30)
        {
            findings.Add($"Average patch time is {statistics.AveragePatchTime:F1} days - consider improving response time");
        }

        if (topVulnerabilities.Any() && topVulnerabilities.First().ComputedRiskScore > 90)
        {
            var top = topVulnerabilities.First();
            findings.Add($"Highest priority: {top.Cve.Id} on {top.Asset.Hostname} (risk score: {top.ComputedRiskScore:F1})");
        }

        return findings;
    }
}
