using Microsoft.Extensions.DependencyInjection;
using PatchMindAI.Core.Interfaces;
using PatchMindAI.Infrastructure.SeedData;

namespace PatchMindAI.Infrastructure.Services;

public sealed class AzureSearchVectorBackfillService : IVectorBackfillService
{
    private readonly IServiceProvider _serviceProvider;

    public AzureSearchVectorBackfillService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public bool IsAvailable => _serviceProvider.GetService<AzureSearchSeeder>() is not null;

    public async Task<int> BackfillAsync(CancellationToken cancellationToken = default)
    {
        var seeder = _serviceProvider.GetService<AzureSearchSeeder>();
        if (seeder is null)
        {
            return 0;
        }

        return await seeder.BackfillVectorsAsync(cancellationToken);
    }
}
