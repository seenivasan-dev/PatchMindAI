using System.Text.Json;
using Azure.Messaging.ServiceBus;
using PatchMindAI.Core.Contracts;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Infrastructure.Queues;

public sealed class AzureServiceBusAnalysisJobQueue : IAnalysisJobQueue, IAsyncDisposable
{
    private readonly ServiceBusSender _sender;
    private readonly ServiceBusReceiver _receiver;

    public AzureServiceBusAnalysisJobQueue(ServiceBusClient client, string queueName)
    {
        _sender = client.CreateSender(queueName);
        _receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        });
    }

    public async ValueTask EnqueueAsync(AnalysisRequestMessage request, CancellationToken cancellationToken = default)
    {
        var payload = JsonSerializer.Serialize(request);
        var message = new ServiceBusMessage(payload)
        {
            ContentType = "application/json"
        };

        await _sender.SendMessageAsync(message, cancellationToken);
    }

    public async ValueTask<AnalysisRequestMessage> DequeueAsync(CancellationToken cancellationToken = default)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var message = await _receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30), cancellationToken);
            if (message is null)
            {
                continue;
            }

            var request = JsonSerializer.Deserialize<AnalysisRequestMessage>(message.Body.ToString(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

            if (request is not null)
            {
                return request;
            }
        }

        throw new OperationCanceledException(cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        await _sender.DisposeAsync();
        await _receiver.DisposeAsync();
    }
}
