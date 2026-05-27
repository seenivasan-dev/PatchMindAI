using Azure.Messaging.ServiceBus;
using Azure.Search.Documents;
using Azure;
using Azure.Identity;
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
using PatchMindAI.Infrastructure.Services;
using StackExchange.Redis;

namespace PatchMindAI.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddPatchMindInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<INvdClient, MockNvdClient>();
        services.AddSingleton<ICvePromptResolver, CvePromptResolver>();
        services.Configure<AzureSearchOptions>(configuration.GetSection(AzureSearchOptions.SectionName));

        var azureSearchOptions = configuration.GetSection(AzureSearchOptions.SectionName).Get<AzureSearchOptions>()
            ?? new AzureSearchOptions();

        if (!string.IsNullOrWhiteSpace(azureSearchOptions.Endpoint) && !string.IsNullOrWhiteSpace(azureSearchOptions.IndexName))
        {
            services.AddSingleton(_ => CreateSearchClient(azureSearchOptions));
            services.AddSingleton<IKnowledgeRetriever, AzureSearchKnowledgeRetriever>();
        }
        else
        {
            services.AddSingleton<IKnowledgeRetriever, CveKnowledgeRetriever>();
        }

        // Register DbContext
        var connectionString = configuration.GetConnectionString("PatchMindAIDb") 
            ?? "Data Source=patchmindai.db";
        services.AddDbContext<PatchMindDbContext>(options =>
            options.UseSqlite(connectionString)
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
                services.AddSingleton(_ => new ServiceBusClient(serviceBusOptions.FullyQualifiedNamespace, new DefaultAzureCredential()));
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
            return new SearchClient(endpoint, options.IndexName, new DefaultAzureCredential());
        }

        throw new InvalidOperationException("AzureSearch requires either ApiKey or UseManagedIdentity=true when enabled.");
    }
}
