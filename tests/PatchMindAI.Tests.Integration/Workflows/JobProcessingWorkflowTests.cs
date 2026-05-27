using Microsoft.EntityFrameworkCore;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Enums;
using PatchMindAI.Infrastructure.Data;
using PatchMindAI.Infrastructure.Repositories;

namespace PatchMindAI.Tests.Integration.Workflows;

public class JobProcessingWorkflowTests
{
    private PatchMindDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PatchMindDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new PatchMindDbContext(options);
    }

    [Fact]
    public async Task FullJobLifecycle_ShouldPersistJobAndResult()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var jobRepo = new EfAnalysisJobRepository(context);
        var resultRepo = new EfAnalysisResultRepository(context);

        var jobId = Guid.NewGuid();
        var job = new AnalysisJob
        {
            Id = jobId,
            CveId = "CVE-2021-44228",
            UserQuery = "Analyze this",
            Status = JobStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow
        };

        // Act - Create job
        await jobRepo.CreateAsync(job);
        var createdJob = await jobRepo.GetByIdAsync(jobId);
        Assert.NotNull(createdJob);

        // Act - Update job status to Processing
        job.Status = JobStatus.Processing;
        await jobRepo.UpdateAsync(job);
        var processingJob = await jobRepo.GetByIdAsync(jobId);
        Assert.Equal(JobStatus.Processing, processingJob!.Status);

        // Act - Save result
        var result = new AnalysisResult
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            RiskScore = 9.5,
            RiskJustification = "Critical Log4j vulnerability",
            ImpactSummary = "All Java applications affected",
            AffectedAssetsJson = "[\"Log4j 2.0-beta9 to 2.14.1\"]",
            RemediationStepsJson = "[{\"priority\":\"Critical\",\"action\":\"Update Log4j\"}]",
            RawAgentOutputJson = "{}",
            GeneratedAtUtc = DateTime.UtcNow
        };
        await resultRepo.SaveAsync(result);

        // Act - Update job to Completed
        job.Status = JobStatus.Completed;
        job.CompletedAtUtc = DateTime.UtcNow;
        await jobRepo.UpdateAsync(job);

        // Assert - Verify complete flow
        var finalJob = await jobRepo.GetByIdAsync(jobId);
        Assert.Equal(JobStatus.Completed, finalJob!.Status);
        Assert.NotNull(finalJob.CompletedAtUtc);

        var savedResult = await resultRepo.GetByJobIdAsync(jobId);
        Assert.NotNull(savedResult);
        Assert.Equal(9.5, savedResult.RiskScore);
        Assert.Equal("Critical Log4j vulnerability", savedResult.RiskJustification);
    }

    [Fact]
    public async Task FailedJob_ShouldPersistFailureReason()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var jobRepo = new EfAnalysisJobRepository(context);

        var jobId = Guid.NewGuid();
        var job = new AnalysisJob
        {
            Id = jobId,
            CveId = "CVE-2021-44228",
            Status = JobStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow
        };
        await jobRepo.CreateAsync(job);

        // Act - Simulate failure
        job.Status = JobStatus.Failed;
        job.FailureReason = "CVE database unavailable";
        job.CompletedAtUtc = DateTime.UtcNow;
        await jobRepo.UpdateAsync(job);

        // Assert
        var failedJob = await jobRepo.GetByIdAsync(jobId);
        Assert.NotNull(failedJob);
        Assert.Equal(JobStatus.Failed, failedJob.Status);
        Assert.Equal("CVE database unavailable", failedJob.FailureReason);
    }

    [Fact]
    public async Task MultipleJobs_ShouldIsolateProperly()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var jobRepo = new EfAnalysisJobRepository(context);
        var resultRepo = new EfAnalysisResultRepository(context);

        var job1Id = Guid.NewGuid();
        var job2Id = Guid.NewGuid();

        var job1 = new AnalysisJob { Id = job1Id, CveId = "CVE-2021-44228", Status = JobStatus.Queued, CreatedAtUtc = DateTime.UtcNow };
        var job2 = new AnalysisJob { Id = job2Id, CveId = "CVE-2023-123456", Status = JobStatus.Queued, CreatedAtUtc = DateTime.UtcNow };

        // Act
        await jobRepo.CreateAsync(job1);
        await jobRepo.CreateAsync(job2);

        var result1 = new AnalysisResult
        {
            Id = Guid.NewGuid(),
            JobId = job1Id,
            RiskScore = 9.5,
            RiskJustification = "Critical",
            ImpactSummary = "High",
            AffectedAssetsJson = "[]",
            RemediationStepsJson = "[]",
            RawAgentOutputJson = "{}",
            GeneratedAtUtc = DateTime.UtcNow
        };

        var result2 = new AnalysisResult
        {
            Id = Guid.NewGuid(),
            JobId = job2Id,
            RiskScore = 5.0,
            RiskJustification = "Medium",
            ImpactSummary = "Medium",
            AffectedAssetsJson = "[]",
            RemediationStepsJson = "[]",
            RawAgentOutputJson = "{}",
            GeneratedAtUtc = DateTime.UtcNow
        };

        await resultRepo.SaveAsync(result1);
        await resultRepo.SaveAsync(result2);

        // Assert
        var retrieved1 = await resultRepo.GetByJobIdAsync(job1Id);
        var retrieved2 = await resultRepo.GetByJobIdAsync(job2Id);

        Assert.Equal(9.5, retrieved1!.RiskScore);
        Assert.Equal(5.0, retrieved2!.RiskScore);
        Assert.NotEqual(retrieved1.Id, retrieved2.Id);
    }
}
