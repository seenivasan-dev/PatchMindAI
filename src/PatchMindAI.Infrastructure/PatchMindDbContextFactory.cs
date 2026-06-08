using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using PatchMindAI.Infrastructure.Data;

namespace PatchMindAI.Infrastructure;

/// <summary>
/// Design-time factory for EF Core migrations.
/// Reads connection string from appsettings.json or environment variable.
/// </summary>
public class PatchMindDbContextFactory : IDesignTimeDbContextFactory<PatchMindDbContext>
{
    public PatchMindDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PatchMindDbContext>();
        
        // Try environment variable first
        var connectionString = Environment.GetEnvironmentVariable("PATCHMINDAI_DB_CONNECTION");
        
        // If not set, try to read from appsettings.json in API project
        if (string.IsNullOrEmpty(connectionString))
        {
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Path.Combine(Directory.GetCurrentDirectory(), "../PatchMindAI.API"))
                .AddJsonFile("appsettings.json", optional: false)
                .Build();
            
            connectionString = configuration.GetConnectionString("PatchMindAIDb");
        }
        
        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string not found. Set PATCHMINDAI_DB_CONNECTION environment variable or configure PatchMindAIDb in appsettings.json");
        }
        
        optionsBuilder.UseSqlServer(connectionString, sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null);
        });
        return new PatchMindDbContext(optionsBuilder.Options);
    }
}
