using Microsoft.Extensions.DependencyInjection;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.API.Background;

public sealed class AnalysisJobWorker : BackgroundService
{
    private readonly IAnalysisJobQueue _queue;
    private readonly IAnalysisCache _cache;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AnalysisJobWorker> _logger;

    public AnalysisJobWorker(
        IAnalysisJobQueue queue,
        IAnalysisCache cache,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<AnalysisJobWorker> logger)
    {
        _queue = queue;
        _cache = cache;
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Analysis worker started.");
        
        // Start Service Bus processor if using Azure Service Bus
        if (_queue is Infrastructure.Queues.AzureServiceBusAnalysisJobQueue azureQueue)
        {
            await azureQueue.StartProcessingAsync();
        }

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var message = await _queue.DequeueAsync(stoppingToken);
                
                using (var scope = _serviceScopeFactory.CreateScope())
                {
                    var jobRepository = scope.ServiceProvider.GetRequiredService<IAnalysisJobRepository>();
                    var resultRepository = scope.ServiceProvider.GetRequiredService<IAnalysisResultRepository>();
                    var orchestrator = scope.ServiceProvider.GetRequiredService<IAnalysisOrchestrator>();

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
                        
                        // Retry logic with exponential backoff for rate limiting
                        var result = await RetryWithBackoffAsync(
                            async () => await orchestrator.RunAsync(job, stoppingToken),
                            maxRetries: 3,
                            stoppingToken);

                        await resultRepository.SaveAsync(result, stoppingToken);
                        await _cache.SetResultAsync(result, stoppingToken);

                        job.Status = JobStatus.Completed;
                        job.CompletedAtUtc = DateTime.UtcNow;
                        await jobRepository.UpdateAsync(job, stoppingToken);
                        await _cache.SetJobAsync(job, stoppingToken);
                        
                        // Complete the message in Service Bus
                        if (_queue is Infrastructure.Queues.AzureServiceBusAnalysisJobQueue azureBusQueue)
                        {
                            await azureBusQueue.CompleteMessageAsync(job.Id);
                        }

                        _logger.LogInformation("Completed analysis job {JobId}", job.Id);
                    }
                    catch (Exception ex) when (IsTransientError(ex))
                    {
                        // Transient error - let Service Bus retry
                        _logger.LogWarning(ex, "Transient error processing job {JobId}, will retry", job.Id);
                        job.Status = JobStatus.Queued; // Reset to queued for retry
                        await jobRepository.UpdateAsync(job, stoppingToken);
                        
                        // Abandon message for Service Bus retry (if applicable)
                        if (_queue is Infrastructure.Queues.AzureServiceBusAnalysisJobQueue azureBusRetry)
                        {
                            await azureBusRetry.AbandonMessageAsync(job.Id, ex.Message);
                        }
                        else
                        {
                            // Re-enqueue for InMemory
                            await _queue.EnqueueAsync(message, stoppingToken);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Permanent failure
                        job.Status = JobStatus.Failed;
                        job.FailureReason = ex.Message;
                        job.CompletedAtUtc = DateTime.UtcNow;
                        await jobRepository.UpdateAsync(job, stoppingToken);
                        await _cache.SetJobAsync(job, stoppingToken);

                        _logger.LogError(ex, "Failed analysis job {JobId}", job.Id);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when the application is shutting down
            _logger.LogInformation("Analysis worker received cancellation request.");
        }

        _logger.LogInformation("Analysis worker stopping.");
    }
    
    private static bool IsTransientError(Exception ex)
    {
        // Check for rate limiting (429) and other transient errors
        return ex is Microsoft.SemanticKernel.HttpOperationException httpEx
            && (httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests
                || httpEx.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable
                || httpEx.StatusCode == System.Net.HttpStatusCode.GatewayTimeout);
    }
    
    private async Task<T> RetryWithBackoffAsync<T>(
        Func<Task<T>> operation,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt < maxRetries)
            {
                // Exponential backoff: 2^attempt seconds (1s, 2s, 4s)
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(
                    "Rate limited (attempt {Attempt}/{MaxRetries}). Waiting {Delay}s before retry...",
                    attempt + 1,
                    maxRetries,
                    delay.TotalSeconds);
                
                await Task.Delay(delay, cancellationToken);
            }
        }
        
        // Final attempt without catching
        return await operation();
    }
}
