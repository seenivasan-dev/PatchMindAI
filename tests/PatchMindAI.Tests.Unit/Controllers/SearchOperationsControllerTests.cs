using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using PatchMindAI.API.Controllers;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Tests.Unit.Controllers;

public class SearchOperationsControllerTests
{
    [Fact]
    public async Task BackfillVectorsAsync_ShouldReturn503_WhenBackfillIsUnavailable()
    {
        var backfillService = new Mock<IVectorBackfillService>();
        backfillService.SetupGet(s => s.IsAvailable).Returns(false);

        var controller = new SearchOperationsController(backfillService.Object);

        var result = await controller.BackfillVectorsAsync(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        backfillService.Verify(s => s.BackfillAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task BackfillVectorsAsync_ShouldReturnOk_WithUpdatedCount_WhenBackfillSucceeds()
    {
        var backfillService = new Mock<IVectorBackfillService>();
        backfillService.SetupGet(s => s.IsAvailable).Returns(true);
        backfillService.Setup(s => s.BackfillAsync(It.IsAny<CancellationToken>())).ReturnsAsync(42);

        var controller = new SearchOperationsController(backfillService.Object);

        var result = await controller.BackfillVectorsAsync(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
        backfillService.Verify(s => s.BackfillAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
