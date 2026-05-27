using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using PatchMindAI.Web.Models;
using PatchMindAI.Web.Models.Analysis;
using PatchMindAI.Web.Services;

namespace PatchMindAI.Web.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly IPatchMindApiClient _apiClient;

    public HomeController(ILogger<HomeController> logger, IPatchMindApiClient apiClient)
    {
        _logger = logger;
        _apiClient = apiClient;
    }

    public IActionResult Index()
    {
        return View(new HomeAnalysisViewModel());
    }

    [HttpPost]
    [Route("home/createjob")]
    public async Task<IActionResult> CreateJobAsync([FromBody] CreateJobRequestModel request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _apiClient.CreateJobAsync(request, cancellationToken);
            return Ok(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create analysis job for {CveId}", request.CveId);
            return BadRequest(new AnalysisErrorResponseModel { Error = ex.Message });
        }
    }

    [HttpPost]
    [Route("home/createprompt")]
    public async Task<IActionResult> CreatePromptAsync([FromBody] AnalyzePromptRequestModel request, CancellationToken cancellationToken)
    {
        try
        {
            var created = await _apiClient.CreatePromptAsync(request, cancellationToken);
            return Ok(created);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create prompt analysis for question {Question}", request.Question);
            return BadRequest(new AnalysisErrorResponseModel { Error = ex.Message });
        }
    }

    [HttpGet]
    [Route("home/jobstatus/{jobId:guid}")]
    public async Task<IActionResult> JobStatusAsync([FromRoute] Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            var status = await _apiClient.GetStatusAsync(jobId, cancellationToken);
            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get status for job {JobId}", jobId);
            return BadRequest(new AnalysisErrorResponseModel { Error = ex.Message });
        }
    }

    [HttpGet]
    [Route("home/jobresult/{jobId:guid}")]
    public async Task<IActionResult> JobResultAsync([FromRoute] Guid jobId, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _apiClient.GetResultAsync(jobId, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get result for job {JobId}", jobId);
            return BadRequest(new AnalysisErrorResponseModel { Error = ex.Message });
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
