using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/cves")]
[Route("api/cves")]
public sealed class CvesController : ControllerBase
{
    private readonly INvdClient _nvdClient;

    public CvesController(INvdClient nvdClient)
    {
        _nvdClient = nvdClient;
    }

    [HttpGet]
    public async Task<IActionResult> GetAsync([FromQuery] string? q, [FromQuery] int? limit, CancellationToken cancellationToken)
    {
        var searchTerm = q ?? string.Empty;
        var pageSize = Math.Clamp(limit ?? 20, 1, 100);
        var cves = await _nvdClient.SearchAsync(searchTerm, pageSize, cancellationToken);
        return Ok(cves);
    }

    [HttpGet("{cveId}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] string cveId, CancellationToken cancellationToken)
    {
        var cve = await _nvdClient.GetCveByIdAsync(cveId, cancellationToken);
        return cve is null ? NotFound() : Ok(cve);
    }
}
