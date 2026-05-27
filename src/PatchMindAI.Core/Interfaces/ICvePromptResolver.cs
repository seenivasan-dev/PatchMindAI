using PatchMindAI.Core.Contracts;

namespace PatchMindAI.Core.Interfaces;

public interface ICvePromptResolver
{
    Task<CvePromptResolution> ResolveAsync(string prompt, CancellationToken cancellationToken = default);
}