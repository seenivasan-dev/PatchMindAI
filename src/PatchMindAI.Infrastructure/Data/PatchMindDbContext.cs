using Microsoft.EntityFrameworkCore;
using PatchMindAI.Core.Domain;

namespace PatchMindAI.Infrastructure.Data;

public sealed class PatchMindDbContext : DbContext
{
    public DbSet<AnalysisJob> AnalysisJobs { get; init; } = null!;

    public DbSet<AnalysisResult> AnalysisResults { get; init; } = null!;

    public DbSet<Cve> Cves { get; init; } = null!;

    public DbSet<Asset> Assets { get; init; } = null!;

    public DbSet<PatchStatus> PatchStatuses { get; init; } = null!;

    public DbSet<AuditLog> AuditLogs { get; init; } = null!;

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

        modelBuilder.Entity<Cve>(entity =>
        {
            entity.HasKey(c => c.Id);
            entity.Property(c => c.Id).HasMaxLength(20);
            entity.Property(c => c.Description).IsRequired().HasMaxLength(5000);
            entity.Property(c => c.VectorString).HasMaxLength(100);
            entity.Property(c => c.Weaknesses).HasConversion(
                v => string.Join(',', v),
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries));
            entity.Property(c => c.AffectedProducts).HasConversion(
                v => string.Join('|', v),
                v => v.Split('|', StringSplitOptions.RemoveEmptyEntries));
            entity.Property(c => c.References).HasConversion(
                v => string.Join('|', v),
                v => v.Split('|', StringSplitOptions.RemoveEmptyEntries));
            entity.Property(c => c.Severity).HasConversion<string>();
            entity.HasIndex(c => c.Severity);
            entity.HasIndex(c => c.BaseScore);
            entity.HasIndex(c => c.PublishedAtUtc);
        });

        modelBuilder.Entity<Asset>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.Hostname).IsRequired().HasMaxLength(255);
            entity.Property(a => a.IpAddress).HasMaxLength(45);
            entity.Property(a => a.OperatingSystem).HasMaxLength(200);
            entity.Property(a => a.Environment).HasMaxLength(50);
            entity.Property(a => a.Location).HasMaxLength(100);
            entity.Property(a => a.Owner).HasMaxLength(200);
            entity.Property(a => a.BusinessUnit).HasMaxLength(200);
            entity.Property(a => a.InstalledSoftware).HasConversion(
                v => string.Join('|', v),
                v => v.Split('|', StringSplitOptions.RemoveEmptyEntries));
            entity.Property(a => a.Type).HasConversion<string>();
            entity.Property(a => a.Criticality).HasConversion<string>();
            entity.HasIndex(a => a.Hostname);
            entity.HasIndex(a => a.Criticality);
            entity.HasIndex(a => a.IsInternetFacing);
            entity.HasIndex(a => a.IsActive);
        });

        modelBuilder.Entity<PatchStatus>(entity =>
        {
            entity.HasKey(ps => ps.Id);
            entity.Property(ps => ps.CveId).IsRequired().HasMaxLength(20);
            entity.Property(ps => ps.AssetId).IsRequired();
            entity.Property(ps => ps.PatchVersion).HasMaxLength(100);
            entity.Property(ps => ps.Notes).HasMaxLength(1000);
            entity.Property(ps => ps.AssignedTo).HasMaxLength(200);
            entity.Property(ps => ps.Status).HasConversion<string>();
            entity.Property(ps => ps.Priority).HasConversion<string>();

            // Configure relationships
            entity.HasOne(ps => ps.Cve)
                .WithMany()
                .HasForeignKey(ps => ps.CveId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(ps => ps.Asset)
                .WithMany(a => a.PatchStatuses)
                .HasForeignKey(ps => ps.AssetId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(ps => ps.CveId);
            entity.HasIndex(ps => ps.AssetId);
            entity.HasIndex(ps => ps.Status);
            entity.HasIndex(ps => ps.Priority);
            entity.HasIndex(ps => new { ps.CveId, ps.AssetId }).IsUnique();
        });

        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(a => a.Id);
            entity.Property(a => a.EventType).IsRequired().HasMaxLength(50);
            entity.Property(a => a.Action).IsRequired().HasMaxLength(100);
            entity.Property(a => a.UserId).HasMaxLength(100);
            entity.Property(a => a.UserQuery).HasMaxLength(2000);
            entity.Property(a => a.JobId).HasMaxLength(50);
            entity.Property(a => a.CveId).HasMaxLength(20);
            entity.Property(a => a.Details).HasMaxLength(2000);
            entity.Property(a => a.IpAddress).HasMaxLength(50);
            entity.Property(a => a.UserAgent).HasMaxLength(500);
            entity.HasIndex(a => a.TimestampUtc);
            entity.HasIndex(a => a.EventType);
            entity.HasIndex(a => a.UserId);
        });
    }
}
