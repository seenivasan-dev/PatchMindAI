using System.Text.Json;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Enums;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Infrastructure.Clients;

public sealed class MockNvdClient : INvdClient
{
    private readonly Lazy<IReadOnlyList<Cve>> _cves;

    public MockNvdClient()
    {
        _cves = new Lazy<IReadOnlyList<Cve>>(LoadSeedData);
    }

    public Task<Cve?> GetCveByIdAsync(string cveId, CancellationToken cancellationToken = default)
    {
        var cve = _cves.Value.FirstOrDefault(item => item.Id.Equals(cveId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(cve);
    }

    public Task<IReadOnlyList<Cve>> SearchAsync(string keyword, int limit = 20, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Task.FromResult<IReadOnlyList<Cve>>(_cves.Value.Take(limit).ToArray());
        }

        var results = _cves.Value
            .Where(item => item.Id.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.Description.Contains(keyword, StringComparison.OrdinalIgnoreCase)
                || item.AffectedProducts.Any(product => product.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
            .Take(limit)
            .ToArray();

        return Task.FromResult<IReadOnlyList<Cve>>(results);
    }

    public Task<IReadOnlyList<RetrievedChunk>> RetrieveAsync(string query, int topK = 5, CancellationToken cancellationToken = default)
    {
        var chunks = SearchAsync(query, topK, cancellationToken)
            .ContinueWith(task => (IReadOnlyList<RetrievedChunk>)task.Result
                .Select(cve => new RetrievedChunk
                {
                    SourceId = cve.Id,
                    Text = $"{cve.Id}: {cve.Description} Impacted products: {string.Join(", ", cve.AffectedProducts)}.",
                    Score = cve.BaseScore
                })
                .ToArray(), cancellationToken);

        return chunks;
    }

    private static IReadOnlyList<Cve> LoadSeedData()
    {
        var seedPath = Path.Combine(AppContext.BaseDirectory, "SeedData", "cve-samples.json");
        var json = File.ReadAllText(seedPath);

        var records = JsonSerializer.Deserialize<List<SeedCve>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        }) ?? [];

        return records.Select(record => new Cve
        {
            Id = record.Id,
            PublishedAtUtc = record.PublishedAtUtc,
            LastModifiedAtUtc = record.LastModifiedAtUtc,
            Description = record.Description,
            BaseScore = record.BaseScore,
            Severity = MapSeverity(record.Severity),
            VectorString = record.VectorString,
            Weaknesses = record.Weaknesses,
            AffectedProducts = record.AffectedProducts,
            References = record.References,
            SyncedAtUtc = DateTime.UtcNow
        }).ToArray();
    }

    private static SeverityLevel MapSeverity(string severity)
    {
        return Enum.TryParse<SeverityLevel>(severity, ignoreCase: true, out var parsed)
            ? parsed
            : SeverityLevel.None;
    }

    private sealed class SeedCve
    {
        public string Id { get; set; } = string.Empty;

        public DateTime PublishedAtUtc { get; set; }

        public DateTime LastModifiedAtUtc { get; set; }

        public string Description { get; set; } = string.Empty;

        public double BaseScore { get; set; }

        public string Severity { get; set; } = string.Empty;

        public string VectorString { get; set; } = string.Empty;

        public string[] Weaknesses { get; set; } = [];

        public string[] AffectedProducts { get; set; } = [];

        public string[] References { get; set; } = [];
    }
}
