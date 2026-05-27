using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PatchMindAI.API.Contracts;
using PatchMindAI.Core.Contracts;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/analysis/jobs")]
[Route("api/analysis/jobs")]
public sealed class AnalysisJobsController : ControllerBase
{
    private readonly INvdClient _nvdClient;
    private readonly IAnalysisJobRepository _jobRepository;
    private readonly IAnalysisJobQueue _queue;
    private readonly IAnalysisResultRepository _resultRepository;
    private readonly IAnalysisCache _cache;

    public AnalysisJobsController(
        INvdClient nvdClient,
        IAnalysisJobRepository jobRepository,
        IAnalysisJobQueue queue,
        IAnalysisResultRepository resultRepository,
        IAnalysisCache cache)
    {
        _nvdClient = nvdClient;
        _jobRepository = jobRepository;
        _queue = queue;
        _resultRepository = resultRepository;
        _cache = cache;
    }

    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateAnalysisJobRequest request, CancellationToken cancellationToken)
    {
        var cve = await _nvdClient.GetCveByIdAsync(request.CveId, cancellationToken);
        if (cve is null)
        {
            return NotFound(new { error = $"CVE '{request.CveId}' was not found." });
        }

        var job = new AnalysisJob
        {
            Id = Guid.NewGuid(),
            CveId = request.CveId,
            UserQuery = request.UserQuery ?? string.Empty,
            Status = JobStatus.Queued,
            CreatedAtUtc = DateTime.UtcNow
        };

        await _jobRepository.CreateAsync(job, cancellationToken);
        await _cache.SetJobAsync(job, cancellationToken);

        await _queue.EnqueueAsync(new AnalysisRequestMessage
        {
            JobId = job.Id,
            CveId = job.CveId,
            UserQuery = job.UserQuery
        }, cancellationToken);

        var response = new AnalysisJobCreatedResponse
        {
            JobId = job.Id,
            Status = job.Status.ToString()
        };

        return Accepted($"/api/analysis/jobs/{job.Id}/status", response);
    }

    [HttpGet("{jobId:guid}/status")]
    public async Task<IActionResult> GetStatusAsync([FromRoute] Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _cache.GetJobAsync(jobId, cancellationToken)
            ?? await _jobRepository.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        await _cache.SetJobAsync(job, cancellationToken);

        var response = new AnalysisJobStatusResponse
        {
            JobId = job.Id,
            CveId = job.CveId,
            Status = job.Status.ToString(),
            CreatedAtUtc = job.CreatedAtUtc,
            CompletedAtUtc = job.CompletedAtUtc,
            FailureReason = job.FailureReason
        };

        return Ok(response);
    }

    [HttpGet("{jobId:guid}/result")]
    public async Task<IActionResult> GetResultAsync([FromRoute] Guid jobId, CancellationToken cancellationToken)
    {
        var job = await _cache.GetJobAsync(jobId, cancellationToken)
            ?? await _jobRepository.GetByIdAsync(jobId, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        await _cache.SetJobAsync(job, cancellationToken);

        if (job.Status is JobStatus.Queued or JobStatus.Processing)
        {
            return Accepted($"/api/analysis/jobs/{jobId}/status", new AnalysisJobCreatedResponse
            {
                JobId = jobId,
                Status = job.Status.ToString()
            });
        }

        if (job.Status is JobStatus.Failed)
        {
            return Problem(title: "Analysis failed", detail: job.FailureReason, statusCode: StatusCodes.Status500InternalServerError);
        }

        var result = await _cache.GetResultAsync(jobId, cancellationToken)
            ?? await _resultRepository.GetByJobIdAsync(jobId, cancellationToken);
        if (result is not null)
        {
            await _cache.SetResultAsync(result, cancellationToken);
        }

        return result is null ? NotFound() : Ok(result);
    }
}
