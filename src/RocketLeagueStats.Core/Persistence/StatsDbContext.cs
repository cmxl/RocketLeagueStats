namespace RocketLeagueStats.Core.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

public sealed class StatsDbContext(DbContextOptions<StatsDbContext> options) : DbContext(options)
{
    // DbSets and OnModelCreating filled in Task 6 once entities exist.
}

internal sealed class StatsDbContextDesignTimeFactory : IDesignTimeDbContextFactory<StatsDbContext>
{
    public StatsDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<StatsDbContext>()
            .UseSqlite("Data Source=design.db")
            .Options;

        return new StatsDbContext(options);
    }
}
