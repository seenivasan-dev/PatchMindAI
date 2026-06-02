using System.Text.RegularExpressions;
using PatchMindAI.Core.Contracts;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Infrastructure.Services;

public sealed class CvePromptResolver : ICvePromptResolver
{
    private static readonly Regex CveIdPattern = new(@"CVE-\d{4}-\d{4,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex WordPattern = new(@"[a-z0-9]{3,}", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly IReadOnlyDictionary<string, string> KnownAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["log4shell"] = "CVE-2021-44228",
        ["heartbleed"] = "CVE-2014-0160",
        ["eternalblue"] = "CVE-2017-0144",
        ["printnightmare"] = "CVE-2021-34527",
        ["rapid reset"] = "CVE-2023-44487",
        ["http/2 rapid reset"] = "CVE-2023-44487"
    };

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

        var aliasMatch = await ResolveAliasMatchAsync(prompt, cancellationToken);
        if (aliasMatch is not null)
        {
            return aliasMatch;
        }

        var exactMatch = await ResolveExactMatchAsync(prompt, cancellationToken);
        if (exactMatch is not null)
        {
            return exactMatch;
        }

        var candidates = await _nvdClient.SearchAsync(prompt, 5, cancellationToken);
        if (candidates.Count == 0)
        {
            candidates = await ResolveByTokenSearchAsync(prompt, cancellationToken);
        }

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

    private async Task<CvePromptResolution?> ResolveAliasMatchAsync(string prompt, CancellationToken cancellationToken)
    {
        foreach (var pair in KnownAliases)
        {
            if (!prompt.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cve = await _nvdClient.GetCveByIdAsync(pair.Value, cancellationToken);
            if (cve is null)
            {
                continue;
            }

            return new CvePromptResolution
            {
                IsResolved = true,
                IsExactMatch = false,
                MatchedCveId = cve.Id,
                Confidence = 0.9,
                Explanation = $"Resolved known vulnerability alias '{pair.Key}' to {cve.Id}.",
                CandidateCveIds = new[] { cve.Id }
            };
        }

        return null;
    }

    private async Task<IReadOnlyList<Core.Domain.Cve>> ResolveByTokenSearchAsync(string prompt, CancellationToken cancellationToken)
    {
        var tokens = WordPattern.Matches(prompt)
            .Select(match => match.Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Where(token => token.Length >= 3)
            .Take(8)
            .ToArray();

        if (tokens.Length == 0)
        {
            return Array.Empty<Core.Domain.Cve>();
        }

        var aggregated = new Dictionary<string, Core.Domain.Cve>(StringComparer.OrdinalIgnoreCase);

        foreach (var token in tokens)
        {
            var tokenResults = await _nvdClient.SearchAsync(token, 5, cancellationToken);
            foreach (var cve in tokenResults)
            {
                aggregated.TryAdd(cve.Id, cve);
            }
        }

        return aggregated.Values.ToArray();
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