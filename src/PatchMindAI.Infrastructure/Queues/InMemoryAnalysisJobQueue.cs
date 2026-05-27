using System.Threading.Channels;
using PatchMindAI.Core.Contracts;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Infrastructure.Queues;

public sealed class InMemoryAnalysisJobQueue : IAnalysisJobQueue
{
    private readonly Channel<AnalysisRequestMessage> _channel;

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
        return _channel.Writer.WriteAsync(request, cancellationToken);
    }

    public ValueTask<AnalysisRequestMessage> DequeueAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAsync(cancellationToken);
    }
}
