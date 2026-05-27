using System.Text.Json;
using PatchMindAI.Web.Models.Analysis;

namespace PatchMindAI.Web.Services;

public interface IPatchMindApiClient
{
    Task<AnalysisJobCreatedModel> CreateJobAsync(CreateJobRequestModel request, CancellationToken cancellationToken = default);

    Task<PromptAnalysisCreatedModel> CreatePromptAsync(AnalyzePromptRequestModel request, CancellationToken cancellationToken = default);

    Task<AnalysisJobStatusModel> GetStatusAsync(Guid jobId, CancellationToken cancellationToken = default);

    Task<JsonElement> GetResultAsync(Guid jobId, CancellationToken cancellationToken = default);
}
