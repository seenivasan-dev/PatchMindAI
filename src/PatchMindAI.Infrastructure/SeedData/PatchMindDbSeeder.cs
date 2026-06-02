using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PatchMindAI.Core.Domain;
using PatchMindAI.Core.Enums;
using PatchMindAI.Infrastructure.Data;

namespace PatchMindAI.Infrastructure.SeedData;

public sealed class PatchMindDbSeeder
{
    private readonly PatchMindDbContext _context;
    private readonly ILogger<PatchMindDbSeeder> _logger;

    public PatchMindDbSeeder(PatchMindDbContext context, ILogger<PatchMindDbSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.EnsureCreatedAsync(cancellationToken);

        // Check if data already exists
        if (await _context.Cves.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Database already seeded, skipping...");
            return;
        }

        _logger.LogInformation("Starting database seeding...");

        var cves = GetSeedCves();
        await _context.Cves.AddRangeAsync(cves, cancellationToken);

        var assets = GetSeedAssets();
        await _context.Assets.AddRangeAsync(assets, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        var patchStatuses = GetSeedPatchStatuses(cves, assets);
        await _context.PatchStatuses.AddRangeAsync(patchStatuses, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Database seeding completed. Added {CveCount} CVEs, {AssetCount} assets, {PatchStatusCount} patch statuses",
            cves.Count, assets.Count, patchStatuses.Count);
    }

    private List<Cve> GetSeedCves()
    {
        var now = DateTime.UtcNow;
        return new List<Cve>
        {
            new()
            {
                Id = "CVE-2021-44228",
                PublishedAtUtc = new DateTime(2021, 12, 10, 10, 15, 9, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2023, 11, 7, 8, 15, 20, DateTimeKind.Utc),
                Description = "Apache Log4j2 JNDI features do not protect against attacker controlled LDAP and other endpoints, allowing remote code execution in many configurations.",
                BaseScore = 10.0,
                Severity = SeverityLevel.Critical,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:C/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-502" },
                AffectedProducts = new[] { "Apache Log4j 2.0-beta9 to 2.14.1", "Java applications using vulnerable Log4j2" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2021-44228", "https://logging.apache.org/log4j/2.x/security.html" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2014-0160",
                PublishedAtUtc = new DateTime(2014, 4, 7, 22, 55, 3, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 3, 20, 14, 0, 0, DateTimeKind.Utc),
                Description = "The Heartbeat extension in OpenSSL allows remote attackers to obtain sensitive memory contents from process memory.",
                BaseScore = 7.5,
                Severity = SeverityLevel.High,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:N/A:N",
                Weaknesses = new[] { "CWE-125" },
                AffectedProducts = new[] { "OpenSSL 1.0.1 through 1.0.1f" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2014-0160" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2017-0144",
                PublishedAtUtc = new DateTime(2017, 3, 14, 1, 59, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 5, 10, 11, 40, 0, DateTimeKind.Utc),
                Description = "Microsoft SMBv1 server mishandles specially crafted packets, allowing remote code execution.",
                BaseScore = 8.1,
                Severity = SeverityLevel.High,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-787" },
                AffectedProducts = new[] { "Windows systems with SMBv1 enabled" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2017-0144" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2021-34527",
                PublishedAtUtc = new DateTime(2021, 7, 1, 0, 15, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 4, 12, 9, 0, 0, DateTimeKind.Utc),
                Description = "Windows Print Spooler remote code execution vulnerability that can allow elevation of privileges and remote attacks in some environments.",
                BaseScore = 8.8,
                Severity = SeverityLevel.High,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:U/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-269" },
                AffectedProducts = new[] { "Windows Print Spooler service" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2021-34527" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2023-44487",
                PublishedAtUtc = new DateTime(2023, 10, 10, 12, 15, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 1, 5, 10, 0, 0, DateTimeKind.Utc),
                Description = "HTTP/2 Rapid Reset attack can cause denial of service by rapidly creating and canceling streams.",
                BaseScore = 7.5,
                Severity = SeverityLevel.High,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:N/I:N/A:H",
                Weaknesses = new[] { "CWE-400" },
                AffectedProducts = new[] { "HTTP/2 implementations in web servers and proxies" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2023-44487" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2023-4863",
                PublishedAtUtc = new DateTime(2023, 9, 12, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 2, 14, 0, 0, 0, DateTimeKind.Utc),
                Description = "Heap buffer overflow in WebP image processing that can result in remote code execution with crafted image content.",
                BaseScore = 8.8,
                Severity = SeverityLevel.High,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:R/S:U/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-122" },
                AffectedProducts = new[] { "libwebp versions prior to patched release" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2023-4863" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2024-3094",
                PublishedAtUtc = new DateTime(2024, 3, 29, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 5, 1, 0, 0, 0, DateTimeKind.Utc),
                Description = "Malicious backdoor introduced into xz/liblzma package releases, creating supply-chain compromise risk.",
                BaseScore = 10.0,
                Severity = SeverityLevel.Critical,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:C/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-506" },
                AffectedProducts = new[] { "xz and liblzma impacted package releases" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2024-3094" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2022-22965",
                PublishedAtUtc = new DateTime(2022, 3, 31, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 1, 12, 0, 0, 0, DateTimeKind.Utc),
                Description = "Spring Framework remote code execution vulnerability under specific deployment and classloader conditions.",
                BaseScore = 9.8,
                Severity = SeverityLevel.Critical,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-94" },
                AffectedProducts = new[] { "Spring Framework on JDK 9+ in vulnerable deployments" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2022-22965" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2023-34362",
                PublishedAtUtc = new DateTime(2023, 5, 31, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 4, 3, 0, 0, 0, DateTimeKind.Utc),
                Description = "SQL injection vulnerability in MOVEit Transfer, actively exploited for data theft and unauthorized access.",
                BaseScore = 9.8,
                Severity = SeverityLevel.Critical,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-89" },
                AffectedProducts = new[] { "Progress MOVEit Transfer vulnerable versions" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2023-34362" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2020-0601",
                PublishedAtUtc = new DateTime(2020, 1, 14, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2023, 10, 5, 0, 0, 0, DateTimeKind.Utc),
                Description = "Windows CryptoAPI spoofing vulnerability allows attackers to spoof code-signing certificates.",
                BaseScore = 8.1,
                Severity = SeverityLevel.High,
                VectorString = "CVSS:3.1/AV:N/AC:H/PR:N/UI:N/S:U/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-295" },
                AffectedProducts = new[] { "Windows 10, Windows Server 2016/2019" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2020-0601" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2022-30190",
                PublishedAtUtc = new DateTime(2022, 5, 30, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 2, 8, 0, 0, 0, DateTimeKind.Utc),
                Description = "Microsoft Windows Support Diagnostic Tool (MSDT) remote code execution vulnerability (Follina).",
                BaseScore = 7.8,
                Severity = SeverityLevel.High,
                VectorString = "CVSS:3.1/AV:L/AC:L/PR:N/UI:R/S:U/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-94" },
                AffectedProducts = new[] { "Windows MSDT in multiple Windows versions" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2022-30190" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2023-23397",
                PublishedAtUtc = new DateTime(2023, 3, 14, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 5, 22, 0, 0, 0, DateTimeKind.Utc),
                Description = "Microsoft Outlook elevation of privilege vulnerability exploiting calendar invites for NTLM hash theft.",
                BaseScore = 9.8,
                Severity = SeverityLevel.Critical,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-269" },
                AffectedProducts = new[] { "Microsoft Outlook for Windows" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2023-23397" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2019-0708",
                PublishedAtUtc = new DateTime(2019, 5, 14, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2023, 12, 10, 0, 0, 0, DateTimeKind.Utc),
                Description = "Remote Desktop Services remote code execution vulnerability (BlueKeep) allows wormable attacks.",
                BaseScore = 9.8,
                Severity = SeverityLevel.Critical,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-416" },
                AffectedProducts = new[] { "Windows 7, Windows Server 2008 R2 and older" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2019-0708" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2020-1472",
                PublishedAtUtc = new DateTime(2020, 8, 17, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 3, 5, 0, 0, 0, DateTimeKind.Utc),
                Description = "Netlogon elevation of privilege vulnerability (Zerologon) allows domain controller takeover.",
                BaseScore = 10.0,
                Severity = SeverityLevel.Critical,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:C/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-330" },
                AffectedProducts = new[] { "Windows Server Netlogon service" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2020-1472" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2023-20198",
                PublishedAtUtc = new DateTime(2023, 10, 16, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 4, 15, 0, 0, 0, DateTimeKind.Utc),
                Description = "Cisco IOS XE web UI privilege escalation allowing unauthorized administrative access.",
                BaseScore = 10.0,
                Severity = SeverityLevel.Critical,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:C/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-420" },
                AffectedProducts = new[] { "Cisco IOS XE with web UI exposed" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2023-20198" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2022-26134",
                PublishedAtUtc = new DateTime(2022, 6, 2, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 1, 20, 0, 0, 0, DateTimeKind.Utc),
                Description = "Atlassian Confluence Server and Data Center OGNL injection vulnerability allowing unauthenticated RCE.",
                BaseScore = 9.8,
                Severity = SeverityLevel.Critical,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-917" },
                AffectedProducts = new[] { "Atlassian Confluence Server/Data Center vulnerable versions" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2022-26134" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2021-26855",
                PublishedAtUtc = new DateTime(2021, 3, 2, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 4, 28, 0, 0, 0, DateTimeKind.Utc),
                Description = "Microsoft Exchange Server server-side request forgery (ProxyLogon) allowing authenticated remote code execution.",
                BaseScore = 9.1,
                Severity = SeverityLevel.Critical,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:N",
                Weaknesses = new[] { "CWE-918" },
                AffectedProducts = new[] { "Microsoft Exchange Server 2013/2016/2019" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2021-26855" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2024-21413",
                PublishedAtUtc = new DateTime(2024, 2, 13, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 5, 30, 0, 0, 0, DateTimeKind.Utc),
                Description = "Microsoft Outlook remote code execution vulnerability through specially crafted email hyperlinks.",
                BaseScore = 9.8,
                Severity = SeverityLevel.Critical,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:U/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-94" },
                AffectedProducts = new[] { "Microsoft Outlook various versions" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2024-21413" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2023-46604",
                PublishedAtUtc = new DateTime(2023, 10, 27, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 5, 15, 0, 0, 0, DateTimeKind.Utc),
                Description = "Apache ActiveMQ remote code execution via deserialization of untrusted data from OpenWire protocol.",
                BaseScore = 10.0,
                Severity = SeverityLevel.Critical,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:N/UI:N/S:C/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-502" },
                AffectedProducts = new[] { "Apache ActiveMQ 5.x through 5.18.2, 5.17.5 and earlier" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2023-46604" },
                SyncedAtUtc = now
            },
            new()
            {
                Id = "CVE-2022-41040",
                PublishedAtUtc = new DateTime(2022, 9, 30, 0, 0, 0, DateTimeKind.Utc),
                LastModifiedAtUtc = new DateTime(2024, 3, 18, 0, 0, 0, DateTimeKind.Utc),
                Description = "Microsoft Exchange Server elevation of privilege vulnerability (ProxyNotShell).",
                BaseScore = 8.8,
                Severity = SeverityLevel.High,
                VectorString = "CVSS:3.1/AV:N/AC:L/PR:L/UI:N/S:U/C:H/I:H/A:H",
                Weaknesses = new[] { "CWE-269" },
                AffectedProducts = new[] { "Microsoft Exchange Server 2013/2016/2019" },
                References = new[] { "https://nvd.nist.gov/vuln/detail/CVE-2022-41040" },
                SyncedAtUtc = now
            }
        };
    }

    private List<Asset> GetSeedAssets()
    {
        var now = DateTime.UtcNow;
        return new List<Asset>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "web-prod-01.corp.local",
                IpAddress = "10.0.1.10",
                OperatingSystem = "Ubuntu 22.04 LTS",
                Type = AssetType.WebServer,
                Criticality = AssetCriticality.Critical,
                Environment = "Production",
                Location = "US-East-1",
                IsInternetFacing = true,
                InstalledSoftware = new[] { "Apache 2.4.52", "OpenSSL 1.1.1", "Java 11" },
                Owner = "Platform Team",
                BusinessUnit = "Engineering",
                CreatedAtUtc = now.AddMonths(-6),
                LastScannedAtUtc = now.AddHours(-2),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "web-prod-02.corp.local",
                IpAddress = "10.0.1.11",
                OperatingSystem = "Ubuntu 22.04 LTS",
                Type = AssetType.WebServer,
                Criticality = AssetCriticality.Critical,
                Environment = "Production",
                Location = "US-East-1",
                IsInternetFacing = true,
                InstalledSoftware = new[] { "Apache 2.4.52", "OpenSSL 1.1.1", "Java 11", "Log4j 2.14.1" },
                Owner = "Platform Team",
                BusinessUnit = "Engineering",
                CreatedAtUtc = now.AddMonths(-6),
                LastScannedAtUtc = now.AddHours(-3),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "db-prod-01.corp.local",
                IpAddress = "10.0.2.20",
                OperatingSystem = "Windows Server 2019",
                Type = AssetType.Database,
                Criticality = AssetCriticality.Critical,
                Environment = "Production",
                Location = "US-East-1",
                IsInternetFacing = false,
                InstalledSoftware = new[] { "SQL Server 2019", "Exchange Server 2019" },
                Owner = "Database Team",
                BusinessUnit = "IT Operations",
                CreatedAtUtc = now.AddMonths(-12),
                LastScannedAtUtc = now.AddHours(-1),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "app-prod-01.corp.local",
                IpAddress = "10.0.3.30",
                OperatingSystem = "Red Hat Enterprise Linux 8",
                Type = AssetType.Server,
                Criticality = AssetCriticality.High,
                Environment = "Production",
                Location = "US-West-2",
                IsInternetFacing = false,
                InstalledSoftware = new[] { "Spring Framework 5.3.15", "Apache Tomcat 9.0.60" },
                Owner = "Application Team",
                BusinessUnit = "Product Development",
                CreatedAtUtc = now.AddMonths(-8),
                LastScannedAtUtc = now.AddHours(-4),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "dc-prod-01.corp.local",
                IpAddress = "10.0.4.40",
                OperatingSystem = "Windows Server 2016",
                Type = AssetType.Server,
                Criticality = AssetCriticality.Critical,
                Environment = "Production",
                Location = "US-East-1",
                IsInternetFacing = false,
                InstalledSoftware = new[] { "Active Directory", "Netlogon" },
                Owner = "Infrastructure Team",
                BusinessUnit = "IT Operations",
                CreatedAtUtc = now.AddYears(-2),
                LastScannedAtUtc = now.AddHours(-1),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "web-staging-01.corp.local",
                IpAddress = "10.1.1.50",
                OperatingSystem = "Ubuntu 20.04 LTS",
                Type = AssetType.WebServer,
                Criticality = AssetCriticality.Medium,
                Environment = "Staging",
                Location = "US-West-1",
                IsInternetFacing = true,
                InstalledSoftware = new[] { "Nginx 1.18.0", "Node.js 16.14" },
                Owner = "DevOps Team",
                BusinessUnit = "Engineering",
                CreatedAtUtc = now.AddMonths(-4),
                LastScannedAtUtc = now.AddDays(-1),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "router-edge-01.corp.local",
                IpAddress = "203.0.113.1",
                OperatingSystem = "Cisco IOS XE 17.6.1",
                Type = AssetType.NetworkDevice,
                Criticality = AssetCriticality.Critical,
                Environment = "Production",
                Location = "US-East-1",
                IsInternetFacing = true,
                InstalledSoftware = new[] { "Cisco IOS XE" },
                Owner = "Network Team",
                BusinessUnit = "Infrastructure",
                CreatedAtUtc = now.AddYears(-1),
                LastScannedAtUtc = now.AddHours(-6),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "mail-prod-01.corp.local",
                IpAddress = "10.0.5.50",
                OperatingSystem = "Windows Server 2019",
                Type = AssetType.Server,
                Criticality = AssetCriticality.Critical,
                Environment = "Production",
                Location = "US-Central",
                IsInternetFacing = true,
                InstalledSoftware = new[] { "Microsoft Exchange Server 2019", "Microsoft Outlook" },
                Owner = "Email Team",
                BusinessUnit = "IT Operations",
                CreatedAtUtc = now.AddMonths(-18),
                LastScannedAtUtc = now.AddHours(-2),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "dev-workstation-05.corp.local",
                IpAddress = "10.2.3.105",
                OperatingSystem = "Windows 10 Pro",
                Type = AssetType.Workstation,
                Criticality = AssetCriticality.Low,
                Environment = "Development",
                Location = "US-East-1",
                IsInternetFacing = false,
                InstalledSoftware = new[] { "Visual Studio 2022", "Docker Desktop", "Git" },
                Owner = "Development Team",
                BusinessUnit = "Engineering",
                CreatedAtUtc = now.AddMonths(-3),
                LastScannedAtUtc = now.AddDays(-2),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "backup-prod-01.corp.local",
                IpAddress = "10.0.6.60",
                OperatingSystem = "Ubuntu 22.04 LTS",
                Type = AssetType.Server,
                Criticality = AssetCriticality.High,
                Environment = "Production",
                Location = "US-West-2",
                IsInternetFacing = false,
                InstalledSoftware = new[] { "Veeam Backup", "OpenSSL 1.1.1f" },
                Owner = "Backup Team",
                BusinessUnit = "IT Operations",
                CreatedAtUtc = now.AddMonths(-10),
                LastScannedAtUtc = now.AddHours(-8),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "file-prod-01.corp.local",
                IpAddress = "10.0.7.70",
                OperatingSystem = "Windows Server 2019",
                Type = AssetType.Server,
                Criticality = AssetCriticality.High,
                Environment = "Production",
                Location = "US-East-1",
                IsInternetFacing = false,
                InstalledSoftware = new[] { "MOVEit Transfer 2023.0.0", "IIS 10.0" },
                Owner = "File Transfer Team",
                BusinessUnit = "IT Operations",
                CreatedAtUtc = now.AddMonths(-14),
                LastScannedAtUtc = now.AddHours(-5),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "wiki-prod-01.corp.local",
                IpAddress = "10.0.8.80",
                OperatingSystem = "CentOS 7",
                Type = AssetType.WebServer,
                Criticality = AssetCriticality.Medium,
                Environment = "Production",
                Location = "US-Central",
                IsInternetFacing = true,
                InstalledSoftware = new[] { "Atlassian Confluence 7.18.0", "Apache Tomcat 9.0.62" },
                Owner = "Collaboration Team",
                BusinessUnit = "IT Operations",
                CreatedAtUtc = now.AddMonths(-20),
                LastScannedAtUtc = now.AddHours(-12),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "mq-prod-01.corp.local",
                IpAddress = "10.0.9.90",
                OperatingSystem = "Ubuntu 20.04 LTS",
                Type = AssetType.Server,
                Criticality = AssetCriticality.High,
                Environment = "Production",
                Location = "US-West-1",
                IsInternetFacing = false,
                InstalledSoftware = new[] { "Apache ActiveMQ 5.17.3", "Java 11" },
                Owner = "Messaging Team",
                BusinessUnit = "Infrastructure",
                CreatedAtUtc = now.AddMonths(-9),
                LastScannedAtUtc = now.AddHours(-3),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "rds-gateway-01.corp.local",
                IpAddress = "203.0.113.10",
                OperatingSystem = "Windows Server 2008 R2",
                Type = AssetType.Server,
                Criticality = AssetCriticality.Critical,
                Environment = "Production",
                Location = "US-East-1",
                IsInternetFacing = true,
                InstalledSoftware = new[] { "Remote Desktop Services", "Terminal Services" },
                Owner = "Remote Access Team",
                BusinessUnit = "IT Operations",
                CreatedAtUtc = now.AddYears(-5),
                LastScannedAtUtc = now.AddHours(-1),
                IsActive = true
            },
            new()
            {
                Id = Guid.NewGuid(),
                Hostname = "proxy-prod-01.corp.local",
                IpAddress = "10.0.10.100",
                OperatingSystem = "Ubuntu 22.04 LTS",
                Type = AssetType.LoadBalancer,
                Criticality = AssetCriticality.High,
                Environment = "Production",
                Location = "US-East-1",
                IsInternetFacing = true,
                InstalledSoftware = new[] { "HAProxy 2.6", "HTTP/2 support" },
                Owner = "Infrastructure Team",
                BusinessUnit = "Engineering",
                CreatedAtUtc = now.AddMonths(-7),
                LastScannedAtUtc = now.AddHours(-4),
                IsActive = true
            }
        };
    }

    private List<PatchStatus> GetSeedPatchStatuses(List<Cve> cves, List<Asset> assets)
    {
        var statuses = new List<PatchStatus>();
        var now = DateTime.UtcNow;

        // Log4Shell (CVE-2021-44228) - affects web servers with Java/Log4j
        var log4jCve = cves.First(c => c.Id == "CVE-2021-44228");
        var webProd02 = assets.First(a => a.Hostname == "web-prod-02.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = log4jCve.Id,
            AssetId = webProd02.Id,
            Status = PatchingStatus.Vulnerable,
            Priority = PatchPriority.Emergency,
            DetectedAtUtc = now.AddDays(-30),
            TargetPatchDate = now.AddDays(-25),
            AssignedTo = "Platform Team"
        });

        // Heartbleed (CVE-2014-0160) - affects backup server with old OpenSSL
        var heartbleedCve = cves.First(c => c.Id == "CVE-2014-0160");
        var backupServer = assets.First(a => a.Hostname == "backup-prod-01.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = heartbleedCve.Id,
            AssetId = backupServer.Id,
            Status = PatchingStatus.Patched,
            Priority = PatchPriority.High,
            DetectedAtUtc = now.AddDays(-90),
            PatchedAtUtc = now.AddDays(-85),
            PatchVersion = "OpenSSL 1.1.1s",
            AssignedTo = "Backup Team"
        });

        // EternalBlue (CVE-2017-0144) - affects Windows servers
        var eternalBlueCve = cves.First(c => c.Id == "CVE-2017-0144");
        var dbServer = assets.First(a => a.Hostname == "db-prod-01.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = eternalBlueCve.Id,
            AssetId = dbServer.Id,
            Status = PatchingStatus.Patched,
            Priority = PatchPriority.Critical,
            DetectedAtUtc = now.AddDays(-180),
            PatchedAtUtc = now.AddDays(-175),
            PatchVersion = "KB4012598",
            AssignedTo = "Database Team"
        });

        // PrintNightmare (CVE-2021-34527) - affects Windows servers
        var printNightmareCve = cves.First(c => c.Id == "CVE-2021-34527");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = printNightmareCve.Id,
            AssetId = dbServer.Id,
            Status = PatchingStatus.Patched,
            Priority = PatchPriority.High,
            DetectedAtUtc = now.AddDays(-120),
            PatchedAtUtc = now.AddDays(-115),
            PatchVersion = "KB5004945",
            AssignedTo = "Database Team"
        });

        // HTTP/2 Rapid Reset (CVE-2023-44487) - affects proxy/load balancers
        var http2Cve = cves.First(c => c.Id == "CVE-2023-44487");
        var proxyServer = assets.First(a => a.Hostname == "proxy-prod-01.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = http2Cve.Id,
            AssetId = proxyServer.Id,
            Status = PatchingStatus.InProgress,
            Priority = PatchPriority.High,
            DetectedAtUtc = now.AddDays(-15),
            TargetPatchDate = now.AddDays(3),
            AssignedTo = "Infrastructure Team"
        });

        // Spring4Shell (CVE-2022-22965) - affects Spring app servers
        var spring4ShellCve = cves.First(c => c.Id == "CVE-2022-22965");
        var appServer = assets.First(a => a.Hostname == "app-prod-01.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = spring4ShellCve.Id,
            AssetId = appServer.Id,
            Status = PatchingStatus.Vulnerable,
            Priority = PatchPriority.Critical,
            DetectedAtUtc = now.AddDays(-45),
            TargetPatchDate = now.AddDays(-35),
            AssignedTo = "Application Team",
            Notes = "Application team evaluating impact before patching"
        });

        // MOVEit Transfer (CVE-2023-34362) - affects file transfer server
        var moveitCve = cves.First(c => c.Id == "CVE-2023-34362");
        var fileServer = assets.First(a => a.Hostname == "file-prod-01.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = moveitCve.Id,
            AssetId = fileServer.Id,
            Status = PatchingStatus.Mitigated,
            Priority = PatchPriority.Emergency,
            DetectedAtUtc = now.AddDays(-60),
            TargetPatchDate = now.AddDays(-50),
            AssignedTo = "File Transfer Team",
            Notes = "Workaround applied: disabled HTTP/HTTPS access, awaiting vendor patch"
        });

        // Zerologon (CVE-2020-1472) - affects domain controller
        var zerologonCve = cves.First(c => c.Id == "CVE-2020-1472");
        var dcServer = assets.First(a => a.Hostname == "dc-prod-01.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = zerologonCve.Id,
            AssetId = dcServer.Id,
            Status = PatchingStatus.Patched,
            Priority = PatchPriority.Emergency,
            DetectedAtUtc = now.AddDays(-200),
            PatchedAtUtc = now.AddDays(-195),
            PatchVersion = "KB4571756",
            AssignedTo = "Infrastructure Team"
        });

        // Outlook RCE (CVE-2023-23397) - affects mail server
        var outlookCve = cves.First(c => c.Id == "CVE-2023-23397");
        var mailServer = assets.First(a => a.Hostname == "mail-prod-01.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = outlookCve.Id,
            AssetId = mailServer.Id,
            Status = PatchingStatus.Vulnerable,
            Priority = PatchPriority.Critical,
            DetectedAtUtc = now.AddDays(-20),
            TargetPatchDate = now.AddDays(5),
            AssignedTo = "Email Team"
        });

        // BlueKeep (CVE-2019-0708) - affects RDS gateway
        var blueKeepCve = cves.First(c => c.Id == "CVE-2019-0708");
        var rdsGateway = assets.First(a => a.Hostname == "rds-gateway-01.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = blueKeepCve.Id,
            AssetId = rdsGateway.Id,
            Status = PatchingStatus.Vulnerable,
            Priority = PatchPriority.Emergency,
            DetectedAtUtc = now.AddDays(-100),
            TargetPatchDate = now.AddDays(-90),
            AssignedTo = "Remote Access Team",
            Notes = "Legacy system, patch may break compatibility. Awaiting approval for upgrade."
        });

        // Cisco IOS XE (CVE-2023-20198) - affects network device
        var ciscoCve = cves.First(c => c.Id == "CVE-2023-20198");
        var routerEdge = assets.First(a => a.Hostname == "router-edge-01.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = ciscoCve.Id,
            AssetId = routerEdge.Id,
            Status = PatchingStatus.Patched,
            Priority = PatchPriority.Emergency,
            DetectedAtUtc = now.AddDays(-25),
            PatchedAtUtc = now.AddDays(-20),
            PatchVersion = "IOS XE 17.9.4a",
            AssignedTo = "Network Team"
        });

        // Confluence (CVE-2022-26134) - affects wiki server
        var confluenceCve = cves.First(c => c.Id == "CVE-2022-26134");
        var wikiServer = assets.First(a => a.Hostname == "wiki-prod-01.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = confluenceCve.Id,
            AssetId = wikiServer.Id,
            Status = PatchingStatus.InProgress,
            Priority = PatchPriority.Critical,
            DetectedAtUtc = now.AddDays(-35),
            TargetPatchDate = now.AddDays(2),
            AssignedTo = "Collaboration Team"
        });

        // ProxyLogon (CVE-2021-26855) - affects Exchange server
        var proxyLogonCve = cves.First(c => c.Id == "CVE-2021-26855");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = proxyLogonCve.Id,
            AssetId = mailServer.Id,
            Status = PatchingStatus.Patched,
            Priority = PatchPriority.Emergency,
            DetectedAtUtc = now.AddDays(-150),
            PatchedAtUtc = now.AddDays(-145),
            PatchVersion = "Exchange 2019 CU11",
            AssignedTo = "Email Team"
        });

        // ActiveMQ (CVE-2023-46604) - affects message queue server
        var activeMqCve = cves.First(c => c.Id == "CVE-2023-46604");
        var mqServer = assets.First(a => a.Hostname == "mq-prod-01.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = activeMqCve.Id,
            AssetId = mqServer.Id,
            Status = PatchingStatus.Vulnerable,
            Priority = PatchPriority.Critical,
            DetectedAtUtc = now.AddDays(-10),
            TargetPatchDate = now.AddDays(7),
            AssignedTo = "Messaging Team",
            Notes = "Testing patch in staging environment"
        });

        // XZ backdoor (CVE-2024-3094) - Not applicable to most systems
        var xzCve = cves.First(c => c.Id == "CVE-2024-3094");
        var webStaging = assets.First(a => a.Hostname == "web-staging-01.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = xzCve.Id,
            AssetId = webStaging.Id,
            Status = PatchingStatus.NotApplicable,
            Priority = PatchPriority.Low,
            DetectedAtUtc = now.AddDays(-5),
            AssignedTo = "DevOps Team",
            Notes = "Vulnerability scan confirmed xz package version is safe"
        });

        // WebP (CVE-2023-4863) - affects systems with image processing
        var webpCve = cves.First(c => c.Id == "CVE-2023-4863");
        var webProd01 = assets.First(a => a.Hostname == "web-prod-01.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = webpCve.Id,
            AssetId = webProd01.Id,
            Status = PatchingStatus.Patched,
            Priority = PatchPriority.High,
            DetectedAtUtc = now.AddDays(-40),
            PatchedAtUtc = now.AddDays(-35),
            PatchVersion = "libwebp 1.3.2",
            AssignedTo = "Platform Team"
        });

        // CryptoAPI (CVE-2020-0601) - affects Windows systems
        var cryptoApiCve = cves.First(c => c.Id == "CVE-2020-0601");
        var devWorkstation = assets.First(a => a.Hostname == "dev-workstation-05.corp.local");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = cryptoApiCve.Id,
            AssetId = devWorkstation.Id,
            Status = PatchingStatus.AcceptedRisk,
            Priority = PatchPriority.Medium,
            DetectedAtUtc = now.AddDays(-250),
            AssignedTo = "Development Team",
            Notes = "Development workstation, isolated network, risk accepted by management"
        });

        // MSDT Follina (CVE-2022-30190) - affects Windows systems
        var msdtCve = cves.First(c => c.Id == "CVE-2022-30190");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = msdtCve.Id,
            AssetId = devWorkstation.Id,
            Status = PatchingStatus.Patched,
            Priority = PatchPriority.High,
            DetectedAtUtc = now.AddDays(-80),
            PatchedAtUtc = now.AddDays(-75),
            PatchVersion = "KB5016616",
            AssignedTo = "Development Team"
        });

        // ProxyNotShell (CVE-2022-41040) - affects Exchange server
        var proxyNotShellCve = cves.First(c => c.Id == "CVE-2022-41040");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = proxyNotShellCve.Id,
            AssetId = mailServer.Id,
            Status = PatchingStatus.Patched,
            Priority = PatchPriority.High,
            DetectedAtUtc = now.AddDays(-70),
            PatchedAtUtc = now.AddDays(-65),
            PatchVersion = "Exchange 2019 CU12",
            AssignedTo = "Email Team"
        });

        // Outlook RCE 2024 (CVE-2024-21413) - affects Exchange/Outlook server
        var outlook2024Cve = cves.First(c => c.Id == "CVE-2024-21413");
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = outlook2024Cve.Id,
            AssetId = mailServer.Id,
            Status = PatchingStatus.InProgress,
            Priority = PatchPriority.Critical,
            DetectedAtUtc = now.AddDays(-8),
            TargetPatchDate = now.AddDays(2),
            AssignedTo = "Email Team",
            Notes = "Waiting for maintenance window this weekend"
        });

        // Additional cross-mappings for comprehensive view
        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = log4jCve.Id,
            AssetId = appServer.Id,
            Status = PatchingStatus.Patched,
            Priority = PatchPriority.Emergency,
            DetectedAtUtc = now.AddDays(-30),
            PatchedAtUtc = now.AddDays(-28),
            PatchVersion = "Log4j 2.17.1",
            AssignedTo = "Application Team"
        });

        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = http2Cve.Id,
            AssetId = webProd01.Id,
            Status = PatchingStatus.Patched,
            Priority = PatchPriority.High,
            DetectedAtUtc = now.AddDays(-15),
            PatchedAtUtc = now.AddDays(-12),
            PatchVersion = "Apache 2.4.58",
            AssignedTo = "Platform Team"
        });

        statuses.Add(new PatchStatus
        {
            Id = Guid.NewGuid(),
            CveId = eternalBlueCve.Id,
            AssetId = mailServer.Id,
            Status = PatchingStatus.Patched,
            Priority = PatchPriority.Critical,
            DetectedAtUtc = now.AddDays(-180),
            PatchedAtUtc = now.AddDays(-178),
            PatchVersion = "KB4012598",
            AssignedTo = "Email Team"
        });

        return statuses;
    }
}
