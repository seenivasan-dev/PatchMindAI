using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;
using PatchMindAI.Core.Contracts;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Infrastructure.Queues;

public sealed class AzureServiceBusAnalysisJobQueue : IAnalysisJobQueue, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;
    private readonly ServiceBusProcessor _processor;
    private readonly ILogger<AzureServiceBusAnalysisJobQueue>? _logger;
    private readonly System.Threading.Channels.Channel<(AnalysisRequestMessage Message, ServiceBusReceivedMessage RawMessage)> _messageChannel;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, (ServiceBusReceivedMessage Message, ProcessMessageEventArgs Args)> _pendingMessages;

    public AzureServiceBusAnalysisJobQueue(ServiceBusClient client, string queueName, ILogger<AzureServiceBusAnalysisJobQueue>? logger = null)
    {
        _sender = client.CreateSender(queueName);
        _logger = logger;
        
        // Use PeekLock mode with automatic retries
        _processor = client.CreateProcessor(queueName, new ServiceBusProcessorOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            AutoCompleteMessages = false, // Manual completion after successful processing
            MaxConcurrentCalls = 1, // Process one at a time to avoid rate limits
            MaxAutoLockRenewalDuration = TimeSpan.FromMinutes(5)
        });
        
        _messageChannel = System.Threading.Channels.Channel.CreateUnbounded<(AnalysisRequestMessage, ServiceBusReceivedMessage)>();
        _pendingMessages = new System.Collections.Concurrent.ConcurrentDictionary<Guid, (ServiceBusReceivedMessage, ProcessMessageEventArgs)>();
        
        _processor.ProcessMessageAsync += OnMessageReceived;
        _processor.ProcessErrorAsync += OnError;
    }

    public async Task StartProcessingAsync()
    {
        await _processor.StartProcessingAsync();
        _logger?.LogInformation("Service Bus processor started");
    }
    
    private async Task OnMessageReceived(ProcessMessageEventArgs args)
    {
        try
        {
            var request = JsonSerializer.Deserialize<AnalysisRequestMessage>(args.Message.Body.ToString(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request is not null)
            {
                // Store the message and args for later completion/abandonment
                _pendingMessages[request.JobId] = (args.Message, args);
                await _messageChannel.Writer.WriteAsync((request, args.Message));
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error processing Service Bus message");
            // Abandon the message so it can be retried
            await args.AbandonMessageAsync(args.Message);
        }
    }
    
    private Task OnError(ProcessErrorEventArgs args)
    {
        _logger?.LogError(args.Exception, "Service Bus processor error: {ErrorSource}", args.ErrorSource);
        return Task.CompletedTask;
    }

    public async ValueTask EnqueueAsync(AnalysisRequestMessage request, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(request);
        var message = new ServiceBusMessage(payload)
        {
            ContentType = "application/json",
            MessageId = request.JobId.ToString()
        };

        await _sender.SendMessageAsync(message, cancellationToken);
        _logger?.LogInformation("Enqueued job {JobId} to Service Bus", request.JobId);
    }

    public async ValueTask<AnalysisRequestMessage> DequeueAsync(CancellationToken cancellationToken = default)
    {
        var (request, rawMessage) = await _messageChannel.Reader.ReadAsync(cancellationToken);
        return request;
    }
    
    public async Task CompleteMessageAsync(Guid jobId)
    {
        if (_pendingMessages.TryRemove(jobId, out var pending))
        {
            await pending.Args.CompleteMessageAsync(pending.Message);
            _logger?.LogInformation("Job {JobId} completed and message removed from queue", jobId);
        }
    }
    
    public async Task AbandonMessageAsync(Guid jobId, string reason)
    {
        if (_pendingMessages.TryRemove(jobId, out var pending))
        {
            await pending.Args.AbandonMessageAsync(pending.Message);
            _logger?.LogWarning("Job {JobId} abandoned for retry: {Reason}", jobId, reason);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _processor.StopProcessingAsync();
        await _processor.DisposeAsync();
        await _sender.DisposeAsync();
    }
}
