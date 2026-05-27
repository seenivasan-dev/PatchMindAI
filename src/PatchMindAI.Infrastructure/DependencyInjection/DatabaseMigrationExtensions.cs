using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PatchMindAI.Infrastructure.Data;

namespace PatchMindAI.Infrastructure.DependencyInjection;

public static class DatabaseMigrationExtensions
{
    public static void ApplyPatchMindMigrations(this IServiceCollection services)
    {
        using (var serviceProvider = services.BuildServiceProvider())
        {
            var context = serviceProvider.GetRequiredService<PatchMindDbContext>();
            context.Database.Migrate();
        }
    }
}
