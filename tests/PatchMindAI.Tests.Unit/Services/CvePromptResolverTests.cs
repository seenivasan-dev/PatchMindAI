using Moq;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Infrastructure.Services;

namespace PatchMindAI.Tests.Unit.Services;

public class CvePromptResolverTests
{
    [Fact]
    public async Task ResolveAsync_ShouldResolveExactCveFromPrompt()
    {
        var nvdClient = new Mock<INvdClient>();
        nvdClient
            .Setup(client => client.GetCveByIdAsync("CVE-2021-44228", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Cve { Id = "CVE-2021-44228" });

        var resolver = new CvePromptResolver(nvdClient.Object);

        var result = await resolver.ResolveAsync("Analyze CVE-2021-44228 for impact", CancellationToken.None);

        Assert.True(result.IsResolved);
        Assert.True(result.IsExactMatch);
        Assert.Equal("CVE-2021-44228", result.MatchedCveId);
        Assert.Equal(1.0, result.Confidence);
        nvdClient.Verify(client => client.GetCveByIdAsync("CVE-2021-44228", It.IsAny<CancellationToken>()), Times.Once);
        nvdClient.Verify(client => client.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ResolveAsync_ShouldUseBestSemanticMatchWhenNoExactCveMentioned()
    {
        var nvdClient = new Mock<INvdClient>();
        nvdClient
            .Setup(client => client.SearchAsync("top critical unpatched vulnerabilities", 5, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new Cve { Id = "CVE-2021-26855", BaseScore = 9.8, LastModifiedAtUtc = new DateTime(2024, 1, 1) },
                new Cve { Id = "CVE-2014-0160", BaseScore = 7.5, LastModifiedAtUtc = new DateTime(2024, 3, 1) }
            });

        var resolver = new CvePromptResolver(nvdClient.Object);

        var result = await resolver.ResolveAsync("top critical unpatched vulnerabilities", CancellationToken.None);

        Assert.True(result.IsResolved);
        Assert.False(result.IsExactMatch);
        Assert.Equal("CVE-2021-26855", result.MatchedCveId);
        Assert.Contains("CVE-2021-26855", result.CandidateCveIds);
        Assert.Contains("CVE-2014-0160", result.CandidateCveIds);
        Assert.Equal(0.6, result.Confidence);
        nvdClient.Verify(client => client.SearchAsync("top critical unpatched vulnerabilities", 5, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ResolveAsync_ShouldReturnUnresolvedForEmptyPrompt()
    {
        var nvdClient = new Mock<INvdClient>();
        var resolver = new CvePromptResolver(nvdClient.Object);

        var result = await resolver.ResolveAsync(string.Empty, CancellationToken.None);

        Assert.False(result.IsResolved);
        Assert.Equal(0, result.Confidence);
        Assert.Equal("Prompt was empty.", result.Explanation);
        nvdClient.Verify(client => client.GetCveByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        nvdClient.Verify(client => client.SearchAsync(It.IsAny<string>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}