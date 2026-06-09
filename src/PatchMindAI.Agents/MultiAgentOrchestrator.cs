using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Agents;

/// <summary>
/// Multi-agent orchestrator that routes queries based on classified intent.
/// Delegates to specialized agents: PromptParser, CVE Search, Prioritization, Reporting.
/// </summary>
public sealed class MultiAgentOrchestrator : IAnalysisOrchestrator
{
    private static readonly Regex CveIdPattern = new(@"CVE-\d{4}-\d{4,7}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly IPromptParserAgent _promptParser;
    private readonly IAnalysisOrchestrator _cveOrchestrator;
    private readonly IPrioritizationAgent _prioritizationAgent;
    private readonly IReportAgent _reportAgent;
    private readonly IAuditLogger _auditLogger;
    private readonly ILogger<MultiAgentOrchestrator> _logger;

    public MultiAgentOrchestrator(
        IPromptParserAgent promptParser,
        AzureOpenAiAnalysisOrchestrator cveOrchestrator,
        IPrioritizationAgent prioritizationAgent,
        IReportAgent reportAgent,
        IAuditLogger auditLogger,
        ILogger<MultiAgentOrchestrator> logger)
    {
        _promptParser = promptParser;
        _cveOrchestrator = cveOrchestrator;
        _prioritizationAgent = prioritizationAgent;
        _reportAgent = reportAgent;
        _auditLogger = auditLogger;
        _logger = logger;
    }

    public async Task<AnalysisResult> RunAsync(AnalysisJob job, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("MultiAgentOrchestrator processing job {JobId}: {Query}", job.Id, job.UserQuery);

        // Log the query
        await _auditLogger.LogQueryAsync(job.UserQuery, userId: null, ipAddress: null, cancellationToken);

        try
        {
            // OPTIMIZATION: Skip expensive PromptParser call for direct CVE queries
            // This reduces OpenAI API calls by 50% for the most common use case
            if (CveIdPattern.IsMatch(job.UserQuery))
            {
                _logger.LogInformation("Direct CVE query detected in job {JobId}. Skipping PromptParser to save OpenAI call.", job.Id);
                return await HandleCveSearchAsync(job, new Core.Models.ParsedIntent
                {
                    Intent = QueryIntent.CveSearch,
                    OriginalQuery = job.UserQuery,
                    Confidence = 1.0
                }, cancellationToken);
            }

            // Step 1: Parse the user query to classify intent (only for non-CVE queries)
            var parsedIntent = await _promptParser.ParseAsync(job.UserQuery, cancellationToken);
            
            _logger.LogInformation(
                "Parsed intent: {Intent} (confidence: {Confidence})",
                parsedIntent.Intent,
                parsedIntent.Confidence);

            // Step 2: Route to appropriate agent based on intent
            var result = parsedIntent.Intent switch
            {
                QueryIntent.CveSearch => await HandleCveSearchAsync(job, parsedIntent, cancellationToken),
                QueryIntent.PriorityReport => await HandlePriorityReportAsync(job, parsedIntent, cancellationToken),
                QueryIntent.WeeklySummary => await HandleWeeklySummaryAsync(job, parsedIntent, cancellationToken),
                QueryIntent.AssetInventory => await HandleAssetInventoryAsync(job, parsedIntent, cancellationToken),
                _ => await HandleUnknownIntentAsync(job, cancellationToken)
            };

            // Log successful analysis
            await _auditLogger.LogAnalysisAsync(
                job.Id.ToString(),
                job.CveId,
                "AnalysisCompleted",
                $"Intent: {parsedIntent.Intent}",
                cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in multi-agent orchestration for job {JobId}", job.Id);
            
            await _auditLogger.LogAnalysisAsync(
                job.Id.ToString(),
                job.CveId,
                "AnalysisFailed",
                ex.Message,
                cancellationToken);

            throw;
        }
    }

    private async Task<AnalysisResult> HandleCveSearchAsync(
        AnalysisJob job,
        Core.Models.ParsedIntent parsedIntent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling CVE search for job {JobId}", job.Id);
        
        // Delegate to the existing CVE analysis orchestrator
        return await _cveOrchestrator.RunAsync(job, cancellationToken);
    }

    private async Task<AnalysisResult> HandlePriorityReportAsync(
        AnalysisJob job,
        Core.Models.ParsedIntent parsedIntent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling priority report for job {JobId}", job.Id);

        var topN = parsedIntent.TopN ?? 20;
        var prioritizedVulnerabilities = await _prioritizationAgent.GetPrioritizedVulnerabilitiesAsync(
            topN,
            cancellationToken);

        // Format as analysis result
        var impactSummary = $"Top {prioritizedVulnerabilities.Count} vulnerabilities prioritized by risk score.";
        
        var affectedAssets = prioritizedVulnerabilities
            .Select(v => new { v.Asset.Hostname, v.Cve.Id, RiskScore = v.ComputedRiskScore })
            .Take(10)
            .ToList();

        var remediationSteps = prioritizedVulnerabilities
            .Take(5)
            .Select((v, i) => $"{i + 1}. Patch {v.Cve.Id} on {v.Asset.Hostname} (Risk: {v.ComputedRiskScore:F1})")
            .ToList();

        var avgRiskScore = prioritizedVulnerabilities.Any()
            ? prioritizedVulnerabilities.Average(v => v.ComputedRiskScore)
            : 0.0;

        var riskJustification = prioritizedVulnerabilities.Any()
            ? $"Top vulnerability: {prioritizedVulnerabilities.First().Cve.Id} on {prioritizedVulnerabilities.First().Asset.Hostname} " +
              $"with risk score {prioritizedVulnerabilities.First().ComputedRiskScore:F1}. " +
              $"Average risk score: {avgRiskScore:F1}. " +
              $"{prioritizedVulnerabilities.Count(v => v.IsInternetFacing)} internet-facing assets affected."
            : "No vulnerable systems found.";

        return new AnalysisResult
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            RiskScore = avgRiskScore,
            RiskJustification = riskJustification,
            ImpactSummary = impactSummary,
            AffectedAssetsJson = JsonSerializer.Serialize(affectedAssets),
            RemediationStepsJson = JsonSerializer.Serialize(remediationSteps),
            RawAgentOutputJson = JsonSerializer.Serialize(new
            {
                Intent = "PriorityReport",
                TotalVulnerabilities = prioritizedVulnerabilities.Count,
                TopVulnerabilities = prioritizedVulnerabilities.Take(10).Select(v => new
                {
                    v.Cve.Id,
                    v.Asset.Hostname,
                    v.ComputedRiskScore,
                    v.DaysOpen,
                    v.IsInternetFacing
                })
            }),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private async Task<AnalysisResult> HandleWeeklySummaryAsync(
        AnalysisJob job,
        Core.Models.ParsedIntent parsedIntent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling weekly summary for job {JobId}", job.Id);

        var report = await _reportAgent.GenerateWeeklyReportAsync(
            parsedIntent.TimeRange?.StartDate,
            parsedIntent.TimeRange?.EndDate,
            cancellationToken);

        await _auditLogger.LogReportGenerationAsync(
            "WeeklySummary",
            $"Period: {report.ReportStartDate:yyyy-MM-dd} to {report.ReportEndDate:yyyy-MM-dd}",
            cancellationToken);

        // Convert report to AnalysisResult
        var impactSummary = $"Weekly Report: {report.Statistics.TotalVulnerabilities} total vulnerabilities, " +
                           $"{report.Statistics.CriticalVulnerabilities} critical, " +
                           $"{report.Statistics.PatchedThisWeek} patched this week.";

        var affectedAssets = new
        {
            TotalAffected = report.Statistics.TotalAffectedAssets,
            InternetFacing = report.Statistics.InternetFacingAffected
        };

        var remediationSteps = report.KeyFindings;

        return new AnalysisResult
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            RiskScore = report.Statistics.CriticalVulnerabilities > 0 ? 9.0 : 5.0,
            RiskJustification = report.ExecutiveSummary,
            ImpactSummary = impactSummary,
            AffectedAssetsJson = JsonSerializer.Serialize(affectedAssets),
            RemediationStepsJson = JsonSerializer.Serialize(remediationSteps),
            RawAgentOutputJson = JsonSerializer.Serialize(new
            {
                Intent = "WeeklySummary",
                ReportPeriod = new
                {
                    report.ReportStartDate,
                    report.ReportEndDate,
                    report.GeneratedAtUtc
                },
                Statistics = new
                {
                    report.Statistics.TotalVulnerabilities,
                    report.Statistics.CriticalVulnerabilities,
                    report.Statistics.HighVulnerabilities,
                    report.Statistics.PatchedThisWeek,
                    report.Statistics.NewlyDiscovered,
                    report.Statistics.OverduePatches,
                    report.Statistics.AveragePatchTime,
                    report.Statistics.TotalAffectedAssets,
                    report.Statistics.InternetFacingAffected
                },
                Trends = report.Trends.Select(t => new
                {
                    t.Category,
                    t.Direction,
                    t.ChangePercentage,
                    t.Description
                }).ToList(),
                KeyFindings = report.KeyFindings,
                ExecutiveSummary = report.ExecutiveSummary,
                TopVulnerabilitiesCount = report.TopVulnerabilities.Count
            }),
            GeneratedAtUtc = DateTime.UtcNow
        };
    }

    private Task<AnalysisResult> HandleAssetInventoryAsync(
        AnalysisJob job,
        Core.Models.ParsedIntent parsedIntent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling asset inventory for job {JobId} - not yet fully implemented", job.Id);

        // Placeholder - could be extended to query assets from database
        return Task.FromResult(new AnalysisResult
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            RiskScore = 0.0,
            RiskJustification = "Asset inventory feature is under development.",
            ImpactSummary = "Please use priority report or weekly summary for now.",
            AffectedAssetsJson = "[]",
            RemediationStepsJson = "[]",
            RawAgentOutputJson = JsonSerializer.Serialize(new { Intent = "AssetInventory" }),
            GeneratedAtUtc = DateTime.UtcNow
        });
    }

    private async Task<AnalysisResult> HandleUnknownIntentAsync(
        AnalysisJob job,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Unknown intent for job {JobId}, falling back to CVE search", job.Id);

        // Fall back to CVE search
        return await _cveOrchestrator.RunAsync(job, cancellationToken);
    }
}
