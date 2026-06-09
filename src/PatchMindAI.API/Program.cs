using Asp.Versioning;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PatchMindAI.Agents.DependencyInjection;
using PatchMindAI.API.Background;
using PatchMindAI.API.Health;
using PatchMindAI.API.Middleware;
using PatchMindAI.Core.Configuration;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Infrastructure.Data;
using PatchMindAI.Infrastructure.DependencyInjection;
using PatchMindAI.Infrastructure.SeedData;
var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection(ServiceBusOptions.SectionName));
builder.Services.Configure<RedisOptions>(builder.Configuration.GetSection(RedisOptions.SectionName));
builder.Services.Configure<AzureOpenAIOptions>(builder.Configuration.GetSection(AzureOpenAIOptions.SectionName));
builder.Services.Configure<AzureSearchOptions>(builder.Configuration.GetSection(AzureSearchOptions.SectionName));
builder.Services.Configure<AgentSettings>(builder.Configuration.GetSection(AgentSettings.SectionName));
var azureSearchOptions = builder.Configuration.GetSection(AzureSearchOptions.SectionName).Get<AzureSearchOptions>()
    ?? new AzureSearchOptions();
builder.Services.AddPatchMindInfrastructure(builder.Configuration);
builder.Services.AddPatchMindAgents(builder.Configuration);
builder.Services.AddHostedService(provider =>
{
    var queue = provider.GetRequiredService<IAnalysisJobQueue>();
    var cache = provider.GetRequiredService<IAnalysisCache>();
    var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
    var agentSettings = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<AgentSettings>>();
    var logger = provider.GetRequiredService<ILogger<AnalysisJobWorker>>();
    return new AnalysisJobWorker(queue, cache, scopeFactory, agentSettings, logger);
});
builder.Services.AddControllers();
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var correlationId = context.HttpContext.Items.TryGetValue(CorrelationIdMiddleware.HeaderName, out var value)
            ? value?.ToString()
            : null;

        var problem = new ValidationProblemDetails(context.ModelState)
        {
            Title = "Request validation failed",
            Status = StatusCodes.Status400BadRequest,
            Type = "https://httpstatuses.com/400",
            Instance = context.HttpContext.Request.Path
        };

        problem.Extensions["correlationId"] = correlationId;
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problem);
    };
});
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
})
.AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
builder.Services.AddHealthChecks()
    .AddCheck<ProviderConfigurationHealthCheck>("provider_configuration", tags: ["ready"])
    .AddCheck<SqlConnectivityHealthCheck>("sql_connectivity", tags: ["ready"])
    .AddCheck<AzureOpenAiConnectivityHealthCheck>("openai_connectivity", tags: ["ready"]);

if (!string.IsNullOrWhiteSpace(azureSearchOptions.Endpoint)
    && !string.IsNullOrWhiteSpace(azureSearchOptions.IndexName))
{
    builder.Services.AddHealthChecks()
        .AddCheck<AzureSearchConnectivityHealthCheck>("search_connectivity", tags: ["ready"]);
}

if (!string.IsNullOrWhiteSpace(azureSearchOptions.Endpoint)
    && !string.IsNullOrWhiteSpace(azureSearchOptions.IndexName)
    && azureSearchOptions.EnableVectorSearch)
{
    builder.Services.AddHealthChecks()
        .AddCheck<VectorCoverageHealthCheck>("vector_coverage", tags: ["ready"]);
}
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(
            "https://patchmindai-web.azurewebsites.net",
            "http://localhost:5000",
            "https://localhost:5001")
              .AllowAnyMethod()
              .AllowAnyHeader()
              .AllowCredentials();
    });
});
builder.Services.AddOpenApi();

var app = builder.Build();

// Apply database migrations and seed data on startup - ONLY on first run
// Use a marker file to prevent repeated database checks on cold starts
var setupMarkerPath = Path.Combine("/home", ".patchmindai-setup-complete");
var forceSetup = Environment.GetEnvironmentVariable("FORCE_DB_SETUP") == "true";

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    // Skip setup if marker exists and not forced
    if (File.Exists(setupMarkerPath) && !forceSetup)
    {
        logger.LogInformation("Database setup already completed (marker found). Skipping migrations and seeding for fast cold start.");
    }
    else
    {
        logger.LogInformation("Running first-time database setup...");
        
        var context = scope.ServiceProvider.GetRequiredService<PatchMindDbContext>();
        
        try
        {
            // Apply pending migrations
            var pendingMigrations = await context.Database.GetPendingMigrationsAsync();
            if (pendingMigrations.Any())
            {
                logger.LogInformation("Applying {Count} pending database migrations...", pendingMigrations.Count());
                context.Database.Migrate();
                logger.LogInformation("Database migrations completed.");
            }
            else
            {
                logger.LogInformation("No pending database migrations.");
            }

            // Seed database only if CVE table is empty
            var hasCveData = await context.Cves.AnyAsync();
            if (!hasCveData)
            {
                logger.LogInformation("CVE table is empty. Running database seeder...");
                var dbSeeder = scope.ServiceProvider.GetRequiredService<PatchMindDbSeeder>();
                await dbSeeder.SeedAsync();
                logger.LogInformation("Database seeding completed.");
            }
            else
            {
                logger.LogInformation("CVE data already exists. Skipping database seeding.");
            }

            // Seed Azure Search index if configured
            var searchSeeder = scope.ServiceProvider.GetService<AzureSearchSeeder>();
            if (searchSeeder != null)
            {
                try
                {
                    await searchSeeder.SeedAsync();

                    if (azureSearchOptions.BackfillVectorsOnStartup)
                    {
                        var updated = await searchSeeder.BackfillVectorsAsync();
                        logger.LogInformation("Startup vector backfill completed. Updated {Count} documents.", updated);
                    }
                }
                catch (Exception ex)
                {
                    if (azureSearchOptions.FailStartupOnSeedError)
                    {
                        logger.LogError(ex, "Azure Search seeding failed and FailStartupOnSeedError=true. Stopping startup.");
                        throw;
                    }

                    // Search indexing is best-effort by default and should not block API startup.
                    logger.LogWarning(ex, "Azure Search seeding failed. Continuing startup without blocking API availability.");
                }
            }
            else
            {
                logger.LogInformation("AzureSearchSeeder not registered (Azure Search not configured). Skipping search index seeding.");
            }
            
            // Create marker file to indicate setup is complete
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(setupMarkerPath)!);
                await File.WriteAllTextAsync(setupMarkerPath, $"Setup completed at {DateTime.UtcNow:O}");
                logger.LogInformation("Created setup marker file at {Path}", setupMarkerPath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create setup marker file. Setup will re-run on next cold start.");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Database setup failed during cold start. App will retry on next restart.");
            throw;
        }
    }
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseHttpsRedirection();
app.UseCors("AllowFrontend");

app.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false
});

app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

app.Run();

// Expose Program for WebApplicationFactory in integration tests
public partial class Program { }
