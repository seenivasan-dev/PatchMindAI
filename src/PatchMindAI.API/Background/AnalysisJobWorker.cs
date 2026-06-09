using Microsoft.Extensions.DependencyInjection;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;
using Microsoft.Extensions.Options;
using PatchMindAI.Core.Configuration;

namespace PatchMindAI.API.Background;

public sealed class AnalysisJobWorker : BackgroundService
{
    private readonly IAnalysisJobQueue _queue;
    private readonly IAnalysisCache _cache;
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<AnalysisJobWorker> _logger;
    private readonly AgentSettings _agentSettings;
    private readonly object _circuitLock = new();
    private int _consecutiveTransientFailures;
    private DateTimeOffset? _circuitOpenUntilUtc;

    public AnalysisJobWorker(
        IAnalysisJobQueue queue,
        IAnalysisCache cache,
        IServiceScopeFactory serviceScopeFactory,
        IOptions<AgentSettings> agentSettings,
        ILogger<AnalysisJobWorker> logger)
    {
        _queue = queue;
        _cache = cache;
        _serviceScopeFactory = serviceScopeFactory;
        _agentSettings = agentSettings.Value;
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

        // Cold start warmup delay to avoid immediate rate limiting
        var warmupDelay = TimeSpan.FromSeconds(10);
        _logger.LogInformation("Waiting {Delay}s warmup period before processing jobs to avoid cold start rate limits...", warmupDelay.TotalSeconds);
        await Task.Delay(warmupDelay, stoppingToken);
        _logger.LogInformation("Warmup complete. Ready to process jobs.");

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
                        await CompleteIfServiceBusAsync(message.JobId);
                        continue;
                    }

                    if (job.Status == JobStatus.Completed || job.Status == JobStatus.Failed)
                    {
                        _logger.LogInformation("Suppressed duplicate message for finalized job {JobId} with status {Status}", job.Id, job.Status);
                        await CompleteIfServiceBusAsync(job.Id);
                        continue;
                    }

                    if (job.Status == JobStatus.Processing)
                    {
                        _logger.LogInformation("Suppressed duplicate in-flight message for job {JobId}", job.Id);
                        await CompleteIfServiceBusAsync(job.Id);
                        continue;
                    }

                    if (IsCircuitOpen(out var openUntil))
                    {
                        var waitTime = (openUntil.Value - DateTimeOffset.UtcNow).TotalSeconds;
                        _logger.LogWarning(
                            "OpenAI circuit breaker is open until {OpenUntil}. Waiting {WaitSeconds}s before checking next job.",
                            openUntil,
                            Math.Max(1, waitTime));

                        if (_queue is Infrastructure.Queues.AzureServiceBusAnalysisJobQueue azureBusQueue)
                        {
                            await azureBusQueue.AbandonMessageAsync(job.Id, "Circuit breaker is open");
                        }
                        else
                        {
                            await _queue.EnqueueAsync(message, stoppingToken);
                        }
                        
                        // Wait a bit before processing next message to avoid tight loop
                        await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, waitTime / 2)), stoppingToken);
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
                        RecordSuccess();

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
                        RecordTransientFailure();

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
                        RecordSuccess();

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
        // HttpOperationException from Semantic Kernel doesn't expose StatusCode directly,
        // so we parse the message format: "HTTP {code} (...)"
        if (ex is Microsoft.SemanticKernel.HttpOperationException httpEx)
        {
            var message = httpEx.Message;
            return message.Contains("HTTP 429") || 
                   message.Contains("HTTP 503") || 
                   message.Contains("HTTP 504") ||
                   message.Contains("too_many_requests");
        }
        
        // Also check inner exceptions
        if (ex.InnerException != null)
        {
            return IsTransientError(ex.InnerException);
        }
        
        return false;
    }
    
    private async Task<T> RetryWithBackoffAsync<T>(
        Func<Task<T>> operation,
        int maxRetries,
        CancellationToken cancellationToken)
    {
        for (int attempt = 0; attempt <= maxRetries; attempt++)
        {
            // Check circuit breaker before each attempt
            if (attempt > 0 && IsCircuitOpen(out var openUntil))
            {
                _logger.LogWarning(
                    "Circuit breaker is open until {OpenUntil}. Stopping retries.",
                    openUntil);
                throw new InvalidOperationException($"Circuit breaker is open until {openUntil}");
            }

            try
            {
                return await operation();
            }
            catch (Exception ex) when (IsTransientError(ex) && attempt < maxRetries)
            {
                // On rate limit (429), open circuit immediately to prevent further calls
                if (ex.Message.Contains("HTTP 429") || ex.Message.Contains("too_many_requests"))
                {
                    RecordRateLimitFailure();
                    _logger.LogWarning(
                        "Rate limit hit (attempt {Attempt}/{MaxRetries}). Circuit breaker opened. Job will be requeued.",
                        attempt + 1,
                        maxRetries);
                    throw; // Don't retry, let circuit breaker handle it
                }

                // For other transient errors, use exponential backoff
                var delay = TimeSpan.FromSeconds(Math.Pow(2, attempt));
                _logger.LogWarning(
                    "Transient error (attempt {Attempt}/{MaxRetries}). Waiting {Delay}s before retry...",
                    attempt + 1,
                    maxRetries,
                    delay.TotalSeconds);
                
                await Task.Delay(delay, cancellationToken);
            }
        }
        
        // Final attempt without catching
        return await operation();
    }

    private async Task CompleteIfServiceBusAsync(Guid jobId)
    {
        if (_queue is Infrastructure.Queues.AzureServiceBusAnalysisJobQueue azureBusQueue)
        {
            await azureBusQueue.CompleteMessageAsync(jobId);
        }
    }

    private bool IsCircuitOpen(out DateTimeOffset? openUntil)
    {
        lock (_circuitLock)
        {
            openUntil = _circuitOpenUntilUtc;
            return _circuitOpenUntilUtc.HasValue && _circuitOpenUntilUtc.Value > DateTimeOffset.UtcNow;
        }
    }

    private void RecordRateLimitFailure()
    {
        // Immediately open circuit breaker on rate limit (429)
        lock (_circuitLock)
        {
            _consecutiveTransientFailures = Math.Max(1, _agentSettings.OpenAiCircuitBreakerFailureThreshold);
            // Longer cooldown for rate limits (60 seconds minimum)
            var cooldownSeconds = Math.Max(60, _agentSettings.OpenAiCircuitBreakerCooldownSeconds);
            _circuitOpenUntilUtc = DateTimeOffset.UtcNow.AddSeconds(cooldownSeconds);
            _logger.LogWarning(
                "Circuit breaker OPENED due to rate limit. Will stay open until {OpenUntil} ({CooldownSeconds}s cooldown).",
                _circuitOpenUntilUtc,
                cooldownSeconds);
        }
    }

    private void RecordTransientFailure()
    {
        lock (_circuitLock)
        {
            _consecutiveTransientFailures++;
            var threshold = Math.Max(1, _agentSettings.OpenAiCircuitBreakerFailureThreshold);

            if (_consecutiveTransientFailures >= threshold)
            {
                _circuitOpenUntilUtc = DateTimeOffset.UtcNow.AddSeconds(
                    Math.Max(5, _agentSettings.OpenAiCircuitBreakerCooldownSeconds));
            }
        }
    }

    private void RecordSuccess()
    {
        lock (_circuitLock)
        {
            _consecutiveTransientFailures = 0;
            _circuitOpenUntilUtc = null;
        }
    }
}
