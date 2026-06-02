using Microsoft.EntityFrameworkCore;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Infrastructure.Data;

namespace PatchMindAI.Infrastructure.Repositories;

public sealed class EfCveRepository : INvdClient
{
    private readonly PatchMindDbContext _context;

    public EfCveRepository(PatchMindDbContext context)
    {
        _context = context;
    }

    public async Task<Cve?> GetCveByIdAsync(string cveId, CancellationToken cancellationToken = default)
    {
        return await _context.Cves
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == cveId, cancellationToken);
    }

    public async Task<IReadOnlyList<Cve>> SearchAsync(string keyword, int limit = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return await _context.Cves
                .AsNoTracking()
                .OrderByDescending(c => c.BaseScore)
                .ThenByDescending(c => c.PublishedAtUtc)
                .Take(limit)
                .ToArrayAsync(cancellationToken);
        }

        // Search across multiple fields
        var results = await _context.Cves
            .AsNoTracking()
            .Where(c =>
                EF.Functions.Like(c.Id, $"%{keyword}%") ||
                EF.Functions.Like(c.Description, $"%{keyword}%") ||
                c.AffectedProducts.Any(p => EF.Functions.Like(p, $"%{keyword}%")))
            .OrderByDescending(c => c.BaseScore)
            .ThenByDescending(c => c.PublishedAtUtc)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        return results;
    }

    public async Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        // For DB-backed retrieval, we perform a simple keyword search
        // and convert CVEs to chunks for RAG context
        var cves = await SearchAsync(query, topK, cancellationToken);

        return cves.Select(cve => new RetrievedChunk
        {
            SourceId = cve.Id,
            Text = $"{cve.Id}: {cve.Description} CVSS Score: {cve.BaseScore}. Affected products: {string.Join(", ", cve.AffectedProducts)}. Severity: {cve.Severity}.",
            Score = cve.BaseScore
        }).ToArray();
    }
}
