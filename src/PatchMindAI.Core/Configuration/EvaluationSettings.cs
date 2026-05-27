namespace PatchMindAI.Core.Configuration;

public sealed class EvaluationSettings
{
    public const string SectionName = "EvaluationSettings";

    public bool Enabled { get; set; } = true;

    public double MinPassingScore { get; set; } = 0.8;
}
