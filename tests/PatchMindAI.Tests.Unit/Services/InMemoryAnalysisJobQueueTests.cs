using PatchMindAI.Core.Contracts;
using PatchMindAI.Infrastructure.Queues;

namespace PatchMindAI.Tests.Unit.Services;

public class InMemoryAnalysisJobQueueTests
{
    [Fact]
    public async Task EnqueueAsync_ShouldSuppressDuplicateJobIds_WhilePending()
    {
        var queue = new InMemoryAnalysisJobQueue();
        var jobId = Guid.NewGuid();

        await queue.EnqueueAsync(new AnalysisRequestMessage { JobId = jobId }, CancellationToken.None);
        await queue.EnqueueAsync(new AnalysisRequestMessage { JobId = jobId }, CancellationToken.None);

        var first = await queue.DequeueAsync(CancellationToken.None);
        Assert.Equal(jobId, first.JobId);

        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            _ = await queue.DequeueAsync(cts.Token);
        });
    }
}
