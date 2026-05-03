namespace RocketLeagueStats.WebApi.Tests.Persistence;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Xunit;

public sealed class EventStoreHostingTests : IDisposable
{
    private readonly string dbPath = Path.Combine(
        Path.GetTempPath(),
        $"rl-stats-webapi-{Guid.NewGuid():N}.db");

    [Fact]
    public async Task Host_StartsWithFreshDatabase_AppliesMigrationsAndCreatesFile()
    {
        var connectionString = $"Data Source={this.dbPath}";

        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Stats", connectionString);
                builder.UseSetting("EventStore:Enabled", "false");   // disable writer; only test the migration path
                builder.UseSetting("StatsApi:Port", "0");            // avoid port-binding noise
            });

        // Force host build — the factory builds lazily on first client call.
        using var client = factory.CreateClient();

        Assert.True(File.Exists(this.dbPath), "Migration should have created the database file.");

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();
        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name IN ('Matches','Events','MatchSnapshots','EventParticipants','PlayerMatchStats');";
        var tableCount = (long)(await cmd.ExecuteScalarAsync())!;
        Assert.Equal(5L, tableCount);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        TryDelete(this.dbPath);
        TryDelete(this.dbPath + "-wal");
        TryDelete(this.dbPath + "-shm");
    }

    private static void TryDelete(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
        }
    }
}
