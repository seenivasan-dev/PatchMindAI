namespace PatchMindAI.Core.Interfaces;

public interface IAgentEvaluator
{
    Task<EvaluationSummary> EvaluateAsync(Guid jobId, CancellationToken cancellationToken = default);
}

public sealed class EvaluationSummary
{
    public Guid JobId { get; set; }

    public double AccuracyScore { get; set; }

    public double CompletenessScore { get; set; }

    public double GroundednessScore { get; set; }

    public bool Passed { get; set; }
}
