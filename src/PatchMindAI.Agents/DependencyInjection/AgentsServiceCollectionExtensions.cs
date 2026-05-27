using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Agents.DependencyInjection;

public static class AgentsServiceCollectionExtensions
{
    public static IServiceCollection AddPatchMindAgents(this IServiceCollection services, IConfiguration configuration)
    {
        var azureOpenAiOptions = configuration.GetSection(AzureOpenAIOptions.SectionName).Get<AzureOpenAIOptions>()
            ?? new AzureOpenAIOptions();

        if (!string.IsNullOrWhiteSpace(azureOpenAiOptions.Endpoint) && !string.IsNullOrWhiteSpace(azureOpenAiOptions.DeploymentName))
        {
            services.AddSingleton<IAnalysisOrchestrator, AzureOpenAiAnalysisOrchestrator>();
        }
        else
        {
            services.AddSingleton<IAnalysisOrchestrator, MockAnalysisOrchestrator>();
        }

        return services;
    }
}
