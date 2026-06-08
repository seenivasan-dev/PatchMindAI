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

        var normalizedKeyword = keyword.Trim();
        var pattern = $"%{normalizedKeyword}%";

        // Keep server-side filtering limited to translatable scalar columns.
        var results = await _context.Cves
            .AsNoTracking()
            .Where(c =>
                EF.Functions.Like(c.Id, pattern) ||
                EF.Functions.Like(c.Description, pattern) ||
                EF.Functions.Like(c.VectorString, pattern))
            .OrderByDescending(c => c.BaseScore)
            .ThenByDescending(c => c.PublishedAtUtc)
            .Take(limit)
            .ToArrayAsync(cancellationToken);

        if (results.Length > 0)
        {
            return results;
        }

        // Fallback for converted array columns (e.g., AffectedProducts) that EF can't translate with LIKE.
        // Limit scan size to keep this bounded.
        var productFallbackCandidates = await _context.Cves
            .AsNoTracking()
            .OrderByDescending(c => c.PublishedAtUtc)
            .Take(500)
            .ToArrayAsync(cancellationToken);

        return productFallbackCandidates
            .Where(c => c.AffectedProducts.Any(p => p.Contains(normalizedKeyword, StringComparison.OrdinalIgnoreCase)))
            .OrderByDescending(c => c.BaseScore)
            .ThenByDescending(c => c.PublishedAtUtc)
            .Take(limit)
            .ToArray();
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
