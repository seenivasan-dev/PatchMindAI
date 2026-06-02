using PatchMindAI.Core.Models;

namespace PatchMindAI.Core.Interfaces;

/// <summary>
/// Logs all analysis activities for compliance and audit trails.
/// </summary>
public interface IAuditLogger
{
    /// <summary>
    /// Logs a query event (user submitted a question).
    /// </summary>
    Task LogQueryAsync(
        string userQuery,
        string? userId = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Logs an analysis event (CVE analysis completed).
    /// </summary>
    Task LogAnalysisAsync(
        string jobId,
        string cveId,
        string action,
        string? details = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Logs a report generation event.
    /// </summary>
    Task LogReportGenerationAsync(
        string reportType,
        string? details = null,
        CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Retrieves audit logs for compliance review.
    /// </summary>
    Task<List<AuditLogEntry>> GetAuditLogsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int maxResults = 100,
        CancellationToken cancellationToken = default);
}
