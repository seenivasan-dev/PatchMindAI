using Microsoft.Extensions.DependencyInjection;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.API.Background;

public sealed class AnalysisJobWorker : BackgroundService
{
    private readonly IAnalysisJobQueue _queue;
    private readonly IAnalysisCache _cache;
    private readonly IAnalysisOrchestrator _orchestrator;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AnalysisJobWorker> _logger;

    public AnalysisJobWorker(
        IAnalysisJobQueue queue,
        IAnalysisCache cache,
        IAnalysisOrchestrator orchestrator,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<AnalysisJobWorker> logger)
    {
        _queue = queue;
        _cache = cache;
        _orchestrator = orchestrator;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Analysis worker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            var message = await _queue.DequeueAsync(stoppingToken);
            
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                var jobRepository = scope.ServiceProvider.GetRequiredService<IAnalysisJobRepository>();
                var resultRepository = scope.ServiceProvider.GetRequiredService<IAnalysisResultRepository>();

                var job = await jobRepository.GetByIdAsync(message.JobId, stoppingToken);

                if (job is null)
                {
                    _logger.LogWarning("Skipped message for unknown job {JobId}", message.JobId);
                    continue;
                }

                job.Status = JobStatus.Processing;
                job.FailureReason = null;
                await jobRepository.UpdateAsync(job, stoppingToken);
                await _cache.SetJobAsync(job, stoppingToken);

                try
                {
                    _logger.LogInformation("Processing analysis job {JobId} for CVE {CveId}", job.Id, job.CveId);
                    var result = await _orchestrator.RunAsync(job, stoppingToken);

                    await resultRepository.SaveAsync(result, stoppingToken);
                    await _cache.SetResultAsync(result, stoppingToken);

                    job.Status = JobStatus.Completed;
                    job.CompletedAtUtc = DateTime.UtcNow;
                    await jobRepository.UpdateAsync(job, stoppingToken);
                    await _cache.SetJobAsync(job, stoppingToken);

                    _logger.LogInformation("Completed analysis job {JobId}", job.Id);
                }
                catch (Exception ex)
                {
                    job.Status = JobStatus.Failed;
                    job.FailureReason = ex.Message;
                    job.CompletedAtUtc = DateTime.UtcNow;
                    await jobRepository.UpdateAsync(job, stoppingToken);
                    await _cache.SetJobAsync(job, stoppingToken);

                    _logger.LogError(ex, "Failed analysis job {JobId}", job.Id);
                }
            }
        }

        _logger.LogInformation("Analysis worker stopping.");
    }
}
