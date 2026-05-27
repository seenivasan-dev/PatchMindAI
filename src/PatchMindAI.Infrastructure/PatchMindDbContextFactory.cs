using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using PatchMindAI.Infrastructure.Data;

namespace PatchMindAI.Infrastructure;

public class PatchMindDbContextFactory : IDesignTimeDbContextFactory<PatchMindDbContext>
{
    public PatchMindDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<PatchMindDbContext>();
        optionsBuilder.UseSqlite("Data Source=patchmindai.db");
        return new PatchMindDbContext(optionsBuilder.Options);
    }
}
