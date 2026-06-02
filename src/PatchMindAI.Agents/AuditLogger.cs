using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Core.Models;
using PatchMindAI.Infrastructure.Data;

namespace PatchMindAI.Agents;

/// <summary>
/// Logs all analysis activities for compliance and audit trails.
/// </summary>
public sealed class AuditLogger : IAuditLogger
{
    private readonly PatchMindDbContext _context;
    private readonly ILogger<AuditLogger> _logger;

    public AuditLogger(
        PatchMindDbContext context,
        ILogger<AuditLogger> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task LogQueryAsync(
        string userQuery,
        string? userId = null,
        string? ipAddress = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new Core.Domain.AuditLog
        {
            Id = Guid.NewGuid(),
            TimestampUtc = DateTime.UtcNow,
            EventType = "Query",
            UserId = userId,
            UserQuery = userQuery,
            Action = "UserQuerySubmitted",
            IpAddress = ipAddress
        };

        await SaveAuditLogAsync(entry, cancellationToken);
    }

    public async Task LogAnalysisAsync(
        string jobId,
        string cveId,
        string action,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new Core.Domain.AuditLog
        {
            Id = Guid.NewGuid(),
            TimestampUtc = DateTime.UtcNow,
            EventType = "Analysis",
            JobId = jobId,
            CveId = cveId,
            Action = action,
            Details = details
        };

        await SaveAuditLogAsync(entry, cancellationToken);
    }

    public async Task LogReportGenerationAsync(
        string reportType,
        string? details = null,
        CancellationToken cancellationToken = default)
    {
        var entry = new Core.Domain.AuditLog
        {
            Id = Guid.NewGuid(),
            TimestampUtc = DateTime.UtcNow,
            EventType = "Report",
            Action = $"ReportGenerated_{reportType}",
            Details = details
        };

        await SaveAuditLogAsync(entry, cancellationToken);
    }

    public async Task<List<AuditLogEntry>> GetAuditLogsAsync(
        DateTime? startDate = null,
        DateTime? endDate = null,
        int maxResults = 100,
        CancellationToken cancellationToken = default)
    {
        var query = _context.AuditLogs.AsQueryable();

        if (startDate.HasValue)
        {
            query = query.Where(a => a.TimestampUtc >= startDate.Value);
        }

        if (endDate.HasValue)
        {
            query = query.Where(a => a.TimestampUtc <= endDate.Value);
        }

        var logs = await query
            .OrderByDescending(a => a.TimestampUtc)
            .Take(maxResults)
            .ToListAsync(cancellationToken);

        return logs.Select(log => new AuditLogEntry
        {
            Id = log.Id,
            TimestampUtc = log.TimestampUtc,
            EventType = log.EventType,
            UserId = log.UserId,
            UserQuery = log.UserQuery,
            JobId = log.JobId,
            CveId = log.CveId,
            Action = log.Action,
            Details = log.Details,
            IpAddress = log.IpAddress,
            UserAgent = log.UserAgent
        }).ToList();
    }

    private async Task SaveAuditLogAsync(Core.Domain.AuditLog entry, CancellationToken cancellationToken)
    {
        try
        {
            _context.AuditLogs.Add(entry);
            await _context.SaveChangesAsync(cancellationToken);
            
            _logger.LogInformation(
                "Audit log created: {EventType} - {Action}",
                entry.EventType,
                entry.Action);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save audit log entry");
            // Don't throw - audit logging failure shouldn't break the main flow
        }
    }
}
