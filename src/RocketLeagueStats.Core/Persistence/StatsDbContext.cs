namespace RocketLeagueStats.Core.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class StatsDbContext(DbContextOptions<StatsDbContext> options) : DbContext(options)
{
    // Schema deferred to a future aggregation spec. The DbContext exists so DI registration,
    // connection-string config, and EF tooling (`dotnet ef dbcontext info`) all work today.
}

/// <summary>
/// Design-time factory used by EF Core tooling (dotnet ef dbcontext info/migrations).
/// Uses an in-memory provider so no running SQL Server is needed during development.
/// </summary>
internal sealed class StatsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<StatsDbContext>
{
    public StatsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StatsDbContext>()
            .UseInMemoryDatabase("DesignTime")
            .Options;

        return new StatsDbContext(options);
    }
}
