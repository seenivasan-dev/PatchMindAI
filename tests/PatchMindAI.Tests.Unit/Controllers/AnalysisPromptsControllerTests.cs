using Microsoft.AspNetCore.Mvc;
using Moq;
using PatchMindAI.API.Contracts;
using PatchMindAI.API.Controllers;
using PatchMindAI.Core.Contracts;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Tests.Unit.Controllers;

public class AnalysisPromptsControllerTests
{
    [Fact]
    public async Task CreateAsync_ShouldReturnAccepted_WithCanonicalJobsStatusPath()
    {
        var promptResolver = new Mock<ICvePromptResolver>();
        var nvdClient = new Mock<INvdClient>();
        var jobRepository = new Mock<IAnalysisJobRepository>();
        var queue = new Mock<IAnalysisJobQueue>();
        var resultRepository = new Mock<IAnalysisResultRepository>();
        var cache = new Mock<IAnalysisCache>();

        promptResolver
            .Setup(r => r.ResolveAsync("Tell me about Log4Shell", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CvePromptResolution
            {
                IsResolved = true,
                MatchedCveId = "CVE-2021-44228",
                IsExactMatch = false,
                Confidence = 0.9,
                Explanation = "Resolved known alias",
                CandidateCveIds = new[] { "CVE-2021-44228" }
            });

        nvdClient
            .Setup(c => c.GetCveByIdAsync("CVE-2021-44228", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Cve { Id = "CVE-2021-44228" });

        queue
            .Setup(q => q.EnqueueAsync(It.IsAny<AnalysisRequestMessage>(), It.IsAny<CancellationToken>()))
            .Returns(ValueTask.CompletedTask);

        var controller = new AnalysisPromptsController(
            promptResolver.Object,
            nvdClient.Object,
            jobRepository.Object,
            queue.Object,
            resultRepository.Object,
            cache.Object);

        var result = await controller.CreateAsync(new AnalyzePromptRequest
        {
            Question = "Tell me about Log4Shell"
        }, CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        var payload = Assert.IsType<PromptAnalysisCreatedResponse>(accepted.Value);

        Assert.Equal("Queued", payload.Status);
        Assert.Equal("CVE-2021-44228", payload.MatchedCveId);
        Assert.Equal($"/api/analysis/jobs/{payload.JobId}/status", accepted.Location);

        jobRepository.Verify(r => r.CreateAsync(It.IsAny<AnalysisJob>(), It.IsAny<CancellationToken>()), Times.Once);
        cache.Verify(c => c.SetJobAsync(It.IsAny<AnalysisJob>(), It.IsAny<CancellationToken>()), Times.Once);
        queue.Verify(q => q.EnqueueAsync(It.IsAny<AnalysisRequestMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnUnprocessableEntity_WhenPromptCannotBeResolved()
    {
        var promptResolver = new Mock<ICvePromptResolver>();
        var nvdClient = new Mock<INvdClient>();
        var jobRepository = new Mock<IAnalysisJobRepository>();
        var queue = new Mock<IAnalysisJobQueue>();
        var resultRepository = new Mock<IAnalysisResultRepository>();
        var cache = new Mock<IAnalysisCache>();

        promptResolver
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CvePromptResolution
            {
                IsResolved = false,
                Explanation = "No matching CVE records were found.",
                CandidateCveIds = Array.Empty<string>()
            });

        var controller = new AnalysisPromptsController(
            promptResolver.Object,
            nvdClient.Object,
            jobRepository.Object,
            queue.Object,
            resultRepository.Object,
            cache.Object);

        var result = await controller.CreateAsync(new AnalyzePromptRequest
        {
            Question = "What do I patch first?"
        }, CancellationToken.None);

        Assert.IsType<UnprocessableEntityObjectResult>(result);
        nvdClient.Verify(c => c.GetCveByIdAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        jobRepository.Verify(r => r.CreateAsync(It.IsAny<AnalysisJob>(), It.IsAny<CancellationToken>()), Times.Never);
        queue.Verify(q => q.EnqueueAsync(It.IsAny<AnalysisRequestMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_ShouldReturnNotFound_WhenResolvedCveDoesNotExist()
    {
        var promptResolver = new Mock<ICvePromptResolver>();
        var nvdClient = new Mock<INvdClient>();
        var jobRepository = new Mock<IAnalysisJobRepository>();
        var queue = new Mock<IAnalysisJobQueue>();
        var resultRepository = new Mock<IAnalysisResultRepository>();
        var cache = new Mock<IAnalysisCache>();

        promptResolver
            .Setup(r => r.ResolveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CvePromptResolution
            {
                IsResolved = true,
                MatchedCveId = "CVE-2099-9999",
                Confidence = 0.5,
                Explanation = "Potential match"
            });

        nvdClient
            .Setup(c => c.GetCveByIdAsync("CVE-2099-9999", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Cve?)null);

        var controller = new AnalysisPromptsController(
            promptResolver.Object,
            nvdClient.Object,
            jobRepository.Object,
            queue.Object,
            resultRepository.Object,
            cache.Object);

        var result = await controller.CreateAsync(new AnalyzePromptRequest
        {
            Question = "Unknown vuln alias"
        }, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
        jobRepository.Verify(r => r.CreateAsync(It.IsAny<AnalysisJob>(), It.IsAny<CancellationToken>()), Times.Never);
        queue.Verify(q => q.EnqueueAsync(It.IsAny<AnalysisRequestMessage>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetResultAsync_ShouldReturnAccepted_WithCanonicalJobsStatusPath_WhenPending()
    {
        var promptResolver = new Mock<ICvePromptResolver>();
        var nvdClient = new Mock<INvdClient>();
        var jobRepository = new Mock<IAnalysisJobRepository>();
        var queue = new Mock<IAnalysisJobQueue>();
        var resultRepository = new Mock<IAnalysisResultRepository>();
        var cache = new Mock<IAnalysisCache>();

        var jobId = Guid.NewGuid();
        cache.Setup(c => c.GetJobAsync(jobId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AnalysisJob
            {
                Id = jobId,
                CveId = "CVE-2021-44228",
                UserQuery = "question",
                Status = JobStatus.Processing,
                CreatedAtUtc = DateTime.UtcNow
            });

        var controller = new AnalysisPromptsController(
            promptResolver.Object,
            nvdClient.Object,
            jobRepository.Object,
            queue.Object,
            resultRepository.Object,
            cache.Object);

        var result = await controller.GetResultAsync(jobId, CancellationToken.None);

        var accepted = Assert.IsType<AcceptedResult>(result);
        Assert.Equal($"/api/analysis/jobs/{jobId}/status", accepted.Location);
    }
}
