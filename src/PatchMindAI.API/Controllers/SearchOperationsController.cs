using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.API.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/ops/search")]
[Route("api/ops/search")]
public sealed class SearchOperationsController : ControllerBase
{
    private readonly IVectorBackfillService _vectorBackfillService;

    public SearchOperationsController(IVectorBackfillService vectorBackfillService)
    {
        _vectorBackfillService = vectorBackfillService;
    }

    [HttpPost("backfill-vectors")]
    public async Task<IActionResult> BackfillVectorsAsync(CancellationToken cancellationToken)
    {
        if (!_vectorBackfillService.IsAvailable)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                error = "Azure Search is not configured for vector backfill in this environment."
            });
        }

        var updatedCount = await _vectorBackfillService.BackfillAsync(cancellationToken);
        return Ok(new
        {
            updatedCount,
            status = "Completed"
        });
    }
}
