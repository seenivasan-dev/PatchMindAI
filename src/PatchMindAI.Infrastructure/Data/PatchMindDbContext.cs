using Microsoft.EntityFrameworkCore;
using PatchMindAI.Core.Domain;

namespace PatchMindAI.Infrastructure.Data;

public sealed class PatchMindDbContext : DbContext
{
    public DbSet<AnalysisJob> AnalysisJobs { get; init; } = null!;

    public DbSet<AnalysisResult> AnalysisResults { get; init; } = null!;

    public PatchMindDbContext(DbContextOptions<PatchMindDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AnalysisJob>(entity =>
        {
            entity.HasKey(j => j.Id);
            entity.Property(j => j.CveId).IsRequired().HasMaxLength(20);
            entity.Property(j => j.UserQuery).HasMaxLength(1000);
            entity.Property(j => j.Status).HasConversion<string>();
            entity.Property(j => j.FailureReason).HasMaxLength(500);
            entity.HasIndex(j => j.CveId);
            entity.HasIndex(j => j.Status);
            entity.HasIndex(j => j.CreatedAtUtc);
        });

        modelBuilder.Entity<AnalysisResult>(entity =>
        {
            entity.HasKey(r => r.Id);
            entity.Property(r => r.JobId).IsRequired();
            entity.Property(r => r.RiskJustification).HasMaxLength(1000);
            entity.Property(r => r.ImpactSummary).HasMaxLength(2000);
            entity.Property(r => r.AffectedAssetsJson).HasColumnType("TEXT");
            entity.Property(r => r.RemediationStepsJson).HasColumnType("TEXT");
            entity.Property(r => r.RawAgentOutputJson).HasColumnType("TEXT");
            entity.HasIndex(r => r.JobId).IsUnique();
            entity.HasIndex(r => r.GeneratedAtUtc);
        });
    }
}
