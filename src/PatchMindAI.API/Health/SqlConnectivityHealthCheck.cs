using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using PatchMindAI.Infrastructure.Data;

namespace PatchMindAI.API.Health;

public sealed class SqlConnectivityHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public SqlConnectivityHealthCheck(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PatchMindDbContext>();

        var canConnect = await dbContext.Database.CanConnectAsync(cancellationToken);
        if (!canConnect)
        {
            return HealthCheckResult.Unhealthy("Unable to connect to SQL database.");
        }

        return HealthCheckResult.Healthy("SQL database connectivity is healthy.");
    }
}
