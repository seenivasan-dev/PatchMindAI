using System.Text.RegularExpressions;
using PatchMindAI.Core.Contracts;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Infrastructure.Services;

public sealed class CvePromptResolver : ICvePromptResolver
{
    private static readonly Regex CveIdPattern = new(@"CVE-\d{4}-\d{4,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly INvdClient _nvdClient;

    public CvePromptResolver(INvdClient nvdClient)
    {
        _nvdClient = nvdClient;
    }

    public async Task<CvePromptResolution> ResolveAsync(string prompt, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(prompt))
        {
            return Unresolved("Prompt was empty.");
        }

        var exactMatch = await ResolveExactMatchAsync(prompt, cancellationToken);
        if (exactMatch is not null)
        {
            return exactMatch;
        }

        var candidates = await _nvdClient.SearchAsync(prompt, 5, cancellationToken);
        var orderedCandidates = candidates
            .OrderByDescending(candidate => candidate.BaseScore)
            .ThenByDescending(candidate => candidate.LastModifiedAtUtc)
            .ToArray();

        if (orderedCandidates.Length == 0)
        {
            return Unresolved("No matching CVE records were found.");
        }

        var bestCandidate = orderedCandidates[0];
        return new CvePromptResolution
        {
            IsResolved = true,
            IsExactMatch = false,
            MatchedCveId = bestCandidate.Id,
            Confidence = 0.6,
            Explanation = $"Selected the best semantic match for the prompt: {bestCandidate.Id}.",
            CandidateCveIds = orderedCandidates.Select(candidate => candidate.Id).ToArray()
        };
    }

    private async Task<CvePromptResolution?> ResolveExactMatchAsync(string prompt, CancellationToken cancellationToken)
    {
        var match = CveIdPattern.Match(prompt);
        if (!match.Success)
        {
            return null;
        }

        var cveId = match.Value.ToUpperInvariant();
        var cve = await _nvdClient.GetCveByIdAsync(cveId, cancellationToken);
        if (cve is null)
        {
            return new CvePromptResolution
            {
                IsResolved = false,
                IsExactMatch = true,
                MatchedCveId = cveId,
                Confidence = 0,
                Explanation = $"CVE '{cveId}' was mentioned but not found in the current data set.",
                CandidateCveIds = Array.Empty<string>()
            };
        }

        return new CvePromptResolution
        {
            IsResolved = true,
            IsExactMatch = true,
            MatchedCveId = cve.Id,
            Confidence = 1.0,
            Explanation = $"Resolved exact CVE reference {cve.Id}.",
            CandidateCveIds = new[] { cve.Id }
        };
    }

    private static CvePromptResolution Unresolved(string explanation)
    {
        return new CvePromptResolution
        {
            IsResolved = false,
            IsExactMatch = false,
            MatchedCveId = null,
            Confidence = 0,
            Explanation = explanation,
            CandidateCveIds = Array.Empty<string>()
        };
    }
}