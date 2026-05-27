using Microsoft.EntityFrameworkCore;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Infrastructure.Data;

namespace PatchMindAI.Infrastructure.Repositories;

public sealed class EfAnalysisJobRepository : IAnalysisJobRepository
{
    private readonly PatchMindDbContext _context;

    public EfAnalysisJobRepository(PatchMindDbContext context)
    {
        _context = context;
    }

    public async Task CreateAsync(AnalysisJob job, CancellationToken cancellationToken = default)
    {
        _context.AnalysisJobs.Add(job);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task<AnalysisJob?> GetByIdAsync(Guid jobId, CancellationToken cancellationToken = default)
    {
        return await _context.AnalysisJobs.FirstOrDefaultAsync(j => j.Id == jobId, cancellationToken);
    }

    public async Task UpdateAsync(AnalysisJob job, CancellationToken cancellationToken = default)
    {
        _context.AnalysisJobs.Update(job);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
