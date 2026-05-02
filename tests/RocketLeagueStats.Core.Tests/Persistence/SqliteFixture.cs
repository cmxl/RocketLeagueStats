namespace RocketLeagueStats.Core.Tests.Persistence;

using Microsoft.EntityFrameworkCore;
using RocketLeagueStats.Core.Persistence;

public sealed class SqliteFixture : IAsyncLifetime, IDisposable
{
    public SqliteFixture()
    {
        this.FilePath = Path.Combine(
            Path.GetTempPath(),
            $"rl-stats-tests-{Guid.NewGuid():N}.db");
        this.ConnectionString = $"Data Source={this.FilePath}";
    }

    public string ConnectionString { get; }

    public string FilePath { get; }

    public async Task InitializeAsync()
    {
        await using var ctx = this.CreateDbContext();
        await ctx.Database.MigrateAsync();
    }

    public Task DisposeAsync()
    {
        this.Dispose();
        return Task.CompletedTask;
    }

    public StatsDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<StatsDbContext>()
            .UseSqlite(this.ConnectionString)
            .Options;
        return new StatsDbContext(options);
    }

    public void Dispose()
    {
        // Microsoft.Data.Sqlite pools connections by connection string. EF's DbContext disposes its
        // connection but does NOT remove it from the pool — the file handle stays alive until the
        // pool releases it. ClearAllPools() forces release so the temp .db / .db-wal / .db-shm files
        // can be deleted on Windows (which holds open files exclusively).
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        TryDelete(this.FilePath);
        TryDelete(this.FilePath + "-wal");
        TryDelete(this.FilePath + "-shm");

        GC.SuppressFinalize(this);
    }

    private static void TryDelete(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        try
        {
            File.Delete(path);
        }
        catch (IOException)
        {
            // Best-effort; the temp dir gets cleaned eventually.
        }
    }
}
