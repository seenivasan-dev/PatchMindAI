using Microsoft.EntityFrameworkCore;
using PatchMindAI.Core.Domain;
using PatchMindAI.Infrastructure.Data;
using PatchMindAI.Infrastructure.Repositories;

namespace PatchMindAI.Tests.Unit.Repositories;

public class EfAnalysisResultRepositoryTests
{
    private PatchMindDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<PatchMindDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new PatchMindDbContext(options);
    }

    [Fact]
    public async Task SaveAsync_ShouldPersistResult()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new EfAnalysisResultRepository(context);
        var result = new AnalysisResult
        {
            Id = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            RiskScore = 9.5,
            RiskJustification = "Critical vulnerability",
            ImpactSummary = "High impact on production systems",
            AffectedAssetsJson = "[]",
            RemediationStepsJson = "[]",
            RawAgentOutputJson = "{}",
            GeneratedAtUtc = DateTime.UtcNow
        };

        // Act
        await repository.SaveAsync(result);

        // Assert
        var saved = await context.AnalysisResults.FirstOrDefaultAsync(r => r.Id == result.Id);
        Assert.NotNull(saved);
        Assert.Equal(9.5, saved.RiskScore);
        Assert.Equal("Critical vulnerability", saved.RiskJustification);
    }

    [Fact]
    public async Task GetByJobIdAsync_ShouldReturnResultWhenExists()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new EfAnalysisResultRepository(context);
        var jobId = Guid.NewGuid();
        var result = new AnalysisResult
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            RiskScore = 7.0,
            RiskJustification = "High severity",
            ImpactSummary = "Medium impact",
            AffectedAssetsJson = "[]",
            RemediationStepsJson = "[]",
            RawAgentOutputJson = "{}",
            GeneratedAtUtc = DateTime.UtcNow
        };
        await repository.SaveAsync(result);

        // Act
        var retrieved = await repository.GetByJobIdAsync(jobId);

        // Assert
        Assert.NotNull(retrieved);
        Assert.Equal(jobId, retrieved.JobId);
        Assert.Equal(7.0, retrieved.RiskScore);
    }

    [Fact]
    public async Task GetByJobIdAsync_ShouldReturnNullWhenNotExists()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new EfAnalysisResultRepository(context);

        // Act
        var retrieved = await repository.GetByJobIdAsync(Guid.NewGuid());

        // Assert
        Assert.Null(retrieved);
    }

    [Fact]
    public async Task SaveAsync_ShouldPreserveJsonFields()
    {
        // Arrange
        var context = CreateInMemoryContext();
        var repository = new EfAnalysisResultRepository(context);
        var affectedAssets = "[\"Ubuntu 18.04\",\"Debian 10\"]";
        var remediationSteps = "[{\"priority\":\"Critical\",\"action\":\"Update package\"}]";
        var result = new AnalysisResult
        {
            Id = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            RiskScore = 8.0,
            RiskJustification = "Test",
            ImpactSummary = "Test",
            AffectedAssetsJson = affectedAssets,
            RemediationStepsJson = remediationSteps,
            RawAgentOutputJson = "{}",
            GeneratedAtUtc = DateTime.UtcNow
        };

        // Act
        await repository.SaveAsync(result);

        // Assert
        var saved = await repository.GetByJobIdAsync(result.JobId);
        Assert.NotNull(saved);
        Assert.Equal(affectedAssets, saved.AffectedAssetsJson);
        Assert.Equal(remediationSteps, saved.RemediationStepsJson);
    }
}
