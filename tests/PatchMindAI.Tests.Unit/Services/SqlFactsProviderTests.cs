using Microsoft.EntityFrameworkCore;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Enums;
using PatchMindAI.Infrastructure.Data;
using PatchMindAI.Infrastructure.Services;

namespace PatchMindAI.Tests.Unit.Services;

public class SqlFactsProviderTests
{
    private static PatchMindDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<PatchMindDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new PatchMindDbContext(options);
    }

    [Fact]
    public async Task GetFactsForCveAsync_ShouldReturnCountsAndRankedAssets()
    {
        await using var context = CreateContext();

        var cve = new Cve
        {
            Id = "CVE-2021-44228",
            Description = "Log4Shell",
            BaseScore = 9.8,
            Severity = SeverityLevel.Critical,
            PublishedAtUtc = DateTime.UtcNow,
            LastModifiedAtUtc = DateTime.UtcNow
        };

        var assetCritical = new Asset
        {
            Id = Guid.NewGuid(),
            Hostname = "prod-web-01",
            Criticality = AssetCriticality.Critical,
            IsInternetFacing = true,
            CreatedAtUtc = DateTime.UtcNow,
            LastScannedAtUtc = DateTime.UtcNow,
            InstalledSoftware = Array.Empty<string>()
        };

        var assetMedium = new Asset
        {
            Id = Guid.NewGuid(),
            Hostname = "internal-app-02",
            Criticality = AssetCriticality.Medium,
            IsInternetFacing = false,
            CreatedAtUtc = DateTime.UtcNow,
            LastScannedAtUtc = DateTime.UtcNow,
            InstalledSoftware = Array.Empty<string>()
        };

        context.Cves.Add(cve);
        context.Assets.AddRange(assetCritical, assetMedium);
        context.PatchStatuses.AddRange(
            new PatchStatus
            {
                Id = Guid.NewGuid(),
                CveId = cve.Id,
                AssetId = assetCritical.Id,
                Status = PatchingStatus.Vulnerable,
                DetectedAtUtc = DateTime.UtcNow.AddDays(-20),
                TargetPatchDate = DateTime.UtcNow.AddDays(-5)
            },
            new PatchStatus
            {
                Id = Guid.NewGuid(),
                CveId = cve.Id,
                AssetId = assetMedium.Id,
                Status = PatchingStatus.Vulnerable,
                DetectedAtUtc = DateTime.UtcNow.AddDays(-10),
                TargetPatchDate = DateTime.UtcNow.AddDays(2)
            });

        await context.SaveChangesAsync();

        var provider = new SqlFactsProvider(context);
        var facts = await provider.GetFactsForCveAsync(cve.Id, 10, CancellationToken.None);

        Assert.Equal(cve.Id, facts.CveId);
        Assert.Equal(2, facts.TotalVulnerableAssets);
        Assert.Equal(1, facts.InternetFacingVulnerableAssets);
        Assert.Equal(1, facts.OverduePatches);
        Assert.Equal(1, facts.CriticalAssetsAffected);
        Assert.Equal(0, facts.HighAssetsAffected);
        Assert.True(facts.RankedAssets.Count >= 2);
        Assert.Equal("prod-web-01", facts.RankedAssets.First().Hostname);
    }
}
