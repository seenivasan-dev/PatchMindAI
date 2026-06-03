using Azure.Messaging.ServiceBus;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Infrastructure.Caching;
using PatchMindAI.Infrastructure.Clients;
using PatchMindAI.Infrastructure.Data;
using PatchMindAI.Infrastructure.Queues;
using PatchMindAI.Infrastructure.Repositories;
using PatchMindAI.Infrastructure.SeedData;
using PatchMindAI.Infrastructure.Services;
using StackExchange.Redis;

namespace PatchMindAI.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPatchMindInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var agentSettings = configuration.GetSection(AgentSettings.SectionName).Get<AgentSettings>()
            ?? new AgentSettings();

        // Use EF-backed CVE repository instead of mock
        services.AddScoped<INvdClient, EfCveRepository>();
        services.AddScoped<ICvePromptResolver, CvePromptResolver>();
        services.AddScoped<PatchMindDbSeeder>();
        services.Configure<AzureSearchOptions>(configuration.GetSection(AzureSearchOptions.SectionName));

        var azureSearchOptions = configuration.GetSection(AzureSearchOptions.SectionName).Get<AzureSearchOptions>()
            ?? new AzureSearchOptions();

        if (!string.IsNullOrWhiteSpace(azureSearchOptions.Endpoint) && !string.IsNullOrWhiteSpace(azureSearchOptions.IndexName))
        {
            services.AddSingleton(_ => CreateSearchClient(azureSearchOptions));
            services.AddSingleton(_ => CreateSearchIndexClient(azureSearchOptions));
            services.AddSingleton<IKnowledgeRetriever, AzureSearchKnowledgeRetriever>();
            services.AddScoped<AzureSearchSeeder>();
        }
        else
        {
            if (agentSettings.RequireAzurePipeline)
            {
                throw new InvalidOperationException(
                    "AgentSettings:RequireAzurePipeline is true, but AzureSearch endpoint/index are not fully configured.");
            }

            services.AddSingleton<IKnowledgeRetriever, CveKnowledgeRetriever>();
        }

        // Register DbContext
        var connectionString = configuration.GetConnectionString("PatchMindAIDb") 
            ?? "Server=(localdb)\\mssqllocaldb;Database=PatchMindAI;Trusted_Connection=True;MultipleActiveResultSets=true";
        services.AddDbContext<PatchMindDbContext>(options =>
            options.UseSqlServer(connectionString)
        );

        // Use EF Core repositories (instead of in-memory)
        services.AddScoped<IAnalysisJobRepository, EfAnalysisJobRepository>();
        services.AddScoped<IAnalysisResultRepository, EfAnalysisResultRepository>();

        var redisOptions = configuration.GetSection(RedisOptions.SectionName).Get<RedisOptions>()
            ?? new RedisOptions();

        if (redisOptions.Provider.Equals("Redis", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton(redisOptions);
            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(redisOptions.ConnectionString));
            services.AddSingleton<IAnalysisCache, RedisAnalysisCache>();
        }
        else
        {
            services.AddSingleton<IAnalysisCache, InMemoryAnalysisCache>();
        }

        var serviceBusOptions = configuration.GetSection(ServiceBusOptions.SectionName).Get<ServiceBusOptions>()
            ?? new ServiceBusOptions();

        if (serviceBusOptions.Provider.Equals("AzureServiceBus", StringComparison.OrdinalIgnoreCase))
        {
            if (!string.IsNullOrWhiteSpace(serviceBusOptions.ConnectionString))
            {
                services.AddSingleton(_ => new ServiceBusClient(serviceBusOptions.ConnectionString));
            }
            else if (!string.IsNullOrWhiteSpace(serviceBusOptions.FullyQualifiedNamespace))
            {
                services.AddSingleton(_ => new ServiceBusClient(serviceBusOptions.FullyQualifiedNamespace, new global::Azure.Identity.DefaultAzureCredential()));
            }
            else
            {
                throw new InvalidOperationException("ServiceBus requires either ConnectionString or FullyQualifiedNamespace when Provider is AzureServiceBus.");
            }

            services.AddSingleton<IAnalysisJobQueue>(sp =>
                new AzureServiceBusAnalysisJobQueue(sp.GetRequiredService<ServiceBusClient>(), serviceBusOptions.QueueName));
        }
        else
        {
            services.AddSingleton<IAnalysisJobQueue, InMemoryAnalysisJobQueue>();
        }

        return services;
    }

    private static SearchClient CreateSearchClient(AzureSearchOptions options)
    {
        var endpoint = new Uri(options.Endpoint);

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new SearchClient(endpoint, options.IndexName, new AzureKeyCredential(options.ApiKey));
        }

        if (options.UseManagedIdentity)
        {
            return new SearchClient(endpoint, options.IndexName, CreateTokenCredential());
        }

        throw new InvalidOperationException("AzureSearch requires either ApiKey or UseManagedIdentity=true when enabled.");
    }

    private static SearchIndexClient CreateSearchIndexClient(AzureSearchOptions options)
    {
        var endpoint = new Uri(options.Endpoint);

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new SearchIndexClient(endpoint, new AzureKeyCredential(options.ApiKey));
        }

        if (options.UseManagedIdentity)
        {
            return new SearchIndexClient(endpoint, CreateTokenCredential());
        }

        throw new InvalidOperationException("AzureSearch requires either ApiKey or UseManagedIdentity=true when enabled.");
    }

    private static Azure.Core.TokenCredential CreateTokenCredential()
    {
        return new global::Azure.Identity.ChainedTokenCredential(
            new global::Azure.Identity.AzureCliCredential(),
            new global::Azure.Identity.DefaultAzureCredential(new global::Azure.Identity.DefaultAzureCredentialOptions
            {
                ExcludeAzureCliCredential = true
            }));
    }
}
