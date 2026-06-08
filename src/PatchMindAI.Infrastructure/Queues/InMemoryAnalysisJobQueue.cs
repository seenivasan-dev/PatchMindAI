using System.Threading.Channels;
using PatchMindAI.Core.Contracts;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Infrastructure.Queues;

public sealed class InMemoryAnalysisJobQueue : IAnalysisJobQueue
{
    private readonly Channel<AnalysisRequestMessage> _channel;
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> _pendingJobIds = new();

    public InMemoryAnalysisJobQueue()
    {
        _channel = Channel.CreateUnbounded<AnalysisRequestMessage>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(AnalysisRequestMessage request, CancellationToken cancellationToken = default)
    {
        if (!_pendingJobIds.TryAdd(request.JobId, 0))
        {
            return ValueTask.CompletedTask;
        }

        return _channel.Writer.WriteAsync(request, cancellationToken);
    }

    public ValueTask<AnalysisRequestMessage> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return DequeueAndReleaseAsync(cancellationToken);
    }

    private async ValueTask<AnalysisRequestMessage> DequeueAndReleaseAsync(CancellationToken cancellationToken)
    {
        var request = await _channel.Reader.ReadAsync(cancellationToken);
        _pendingJobIds.TryRemove(request.JobId, out _);
        return request;
    }
}
