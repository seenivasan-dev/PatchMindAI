using Microsoft.EntityFrameworkCore;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Infrastructure.Data;

namespace PatchMindAI.Infrastructure.Repositories;

public sealed class EfAnalysisResultRepository : IAnalysisResultRepository
{
    private readonly PatchMindDbContext _context;

    public EfAnalysisResultRepository(PatchMindDbContext context)
    {
        _context = context;
    }

    public async Task SaveAsync(AnalysisResult result, CancellationToken cancellationToken = default)
    {
        _context.AnalysisResults.Add(result);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AnalysisResult?> GetByJobIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await _context.AnalysisResults.FirstOrDefaultAsync(r => r.JobId == jobId, cancellationToken);
    }
}
