using Microsoft.EntityFrameworkCore;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Enums;
using PatchMindAI.Infrastructure.Data;
using PatchMindAI.Infrastructure.Repositories;

namespace PatchMindAI.Tests.Unit.Repositories;

public class EfAnalysisJobRepositoryTests
{
    private PatchMindDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PatchMindDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new PatchMindDbContext(options);
    }

    [Fact]
    public async Task CreateAsync_ShouldSaveJobToDB()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new EfAnalysisJobRepository(context);
        var job = new AnalysisJob
        {
            Id = Guid.NewGuid(),
            CveId = "CVE-2021-44228",
            UserQuery = "Test query",
            Status = JobStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Act
        await repository.CreateAsync(job);

        // Assert
        var savedJob = await context.AnalysisJobs.FirstOrDefaultAsync(j => j.Id == job.Id);
        Assert.NotNull(savedJob);
        Assert.Equal("CVE-2021-44228", savedJob.CveId);
        Assert.Equal(JobStatus.Queued, savedJob.Status);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnJobWhenExists()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new EfAnalysisJobRepository(context);
        var jobId = Guid.NewGuid();
        var job = new AnalysisJob
        {
            Id = jobId,
            CveId = "CVE-2021-44228",
            UserQuery = "Test",
            Status = JobStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow
        };
        await repository.CreateAsync(job);

        // Act
        var retrieved = await repository.GetByIdAsync(jobId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(jobId, retrieved.Id);
        Assert.Equal("CVE-2021-44228", retrieved.CveId);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNullWhenNotExists()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new EfAnalysisJobRepository(context);
        var jobId = Guid.NewGuid();

        // Act
        var retrieved = await repository.GetByIdAsync(jobId);

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task UpdateAsync_ShouldPersistChanges()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new EfAnalysisJobRepository(context);
        var jobId = Guid.NewGuid();
        var job = new AnalysisJob
        {
            Id = jobId,
            CveId = "CVE-2021-44228",
            UserQuery = "Test",
            Status = JobStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow
        };
        await repository.CreateAsync(job);

        // Act
        job.Status = JobStatus.Processing;
        await repository.UpdateAsync(job);

        // Assert
        var updated = await repository.GetByIdAsync(jobId);
        Assert.NotNull(updated);
        Assert.Equal(JobStatus.Processing, updated.Status);
    }

    [Fact]
    public async Task UpdateAsync_ShouldSetCompletedAtUtc()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new EfAnalysisJobRepository(context);
        var jobId = Guid.NewGuid();
        var completedAt = DateTime.UtcNow;
        var job = new AnalysisJob
        {
            Id = jobId,
            CveId = "CVE-2021-44228",
            Status = JobStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow
        };
        await repository.CreateAsync(job);

        // Act
        job.Status = JobStatus.Completed;
        job.CompletedAtUtc = completedAt;
        await repository.UpdateAsync(job);

        // Assert
        var updated = await repository.GetByIdAsync(jobId);
        Assert.NotNull(updated);
        Assert.Equal(JobStatus.Completed, updated.Status);
        Assert.NotNull(updated.CompletedAtUtc);
    }
}
