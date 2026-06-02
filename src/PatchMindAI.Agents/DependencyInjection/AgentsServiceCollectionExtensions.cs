using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Microsoft.SemanticKernel.ChatCompletion;
using Azure.Identity;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Core.Interfaces;

namespace PatchMindAI.Agents.DependencyInjection;

public static class AgentsServiceCollectionExtensions
{
    public static IServiceCollection AddPatchMindAgents(this IServiceCollection services, IConfiguration configuration)
    {
        var agentSettings = configuration.GetSection(AgentSettings.SectionName).Get<AgentSettings>()
            ?? new AgentSettings();

        var azureOpenAiOptions = configuration.GetSection(AzureOpenAIOptions.SectionName).Get<AzureOpenAIOptions>()
            ?? new AzureOpenAIOptions();

        if (!string.IsNullOrWhiteSpace(azureOpenAiOptions.Endpoint) && !string.IsNullOrWhiteSpace(azureOpenAiOptions.DeploymentName))
        {
            // Register Azure OpenAI Chat Completion Service for all agents
            services.AddScoped<IChatCompletionService>(provider =>
            {
                var options = provider.GetRequiredService<IOptions<AzureOpenAIOptions>>().Value;
                var loggerFactory = provider.GetRequiredService<ILoggerFactory>();

                var apiVersion = string.IsNullOrWhiteSpace(options.ApiVersion)
                    ? null
                    : options.ApiVersion;

                if (!string.IsNullOrWhiteSpace(options.ApiKey))
                {
                    return new AzureOpenAIChatCompletionService(
                        options.DeploymentName,
                        options.Endpoint,
                        options.ApiKey,
                        options.Model,
                        httpClient: null,
                        loggerFactory,
                        apiVersion);
                }

                if (!options.UseManagedIdentity)
                {
                    throw new InvalidOperationException(
                        "AzureOpenAI requires either ApiKey or UseManagedIdentity=true when configured.");
                }

                return new AzureOpenAIChatCompletionService(
                    options.DeploymentName,
                    options.Endpoint,
                    new DefaultAzureCredential(),
                    options.Model,
                    httpClient: null,
                    loggerFactory,
                    apiVersion);
            });

            // Register the base CVE analysis orchestrator
            services.AddScoped<AzureOpenAiAnalysisOrchestrator>();
            
            // If multi-agent is enabled, use MultiAgentOrchestrator as the primary
            if (agentSettings.EnableMultiAgentArchitecture)
            {
                services.AddScoped<IAnalysisOrchestrator, MultiAgentOrchestrator>();
            }
            else
            {
                services.AddScoped<IAnalysisOrchestrator, AzureOpenAiAnalysisOrchestrator>();
            }
        }
        else
        {
            if (agentSettings.RequireAzurePipeline)
            {
                throw new InvalidOperationException(
                    "AgentSettings:RequireAzurePipeline is true, but AzureOpenAI endpoint/deployment is not fully configured.");
            }

            services.AddScoped<IAnalysisOrchestrator, MockAnalysisOrchestrator>();
        }

        // Register multi-agent components
        services.AddScoped<IPromptParserAgent, PromptParserAgent>();
        services.AddScoped<IPrioritizationAgent, PrioritizationAgent>();
        services.AddScoped<IReportAgent, ReportAgent>();
        services.AddScoped<IAuditLogger, AuditLogger>();

        return services;
    }
}
