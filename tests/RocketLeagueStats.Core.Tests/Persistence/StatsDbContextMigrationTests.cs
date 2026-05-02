namespace RocketLeagueStats.Core.Tests.Persistence;

using Microsoft.Data.Sqlite;

public sealed class StatsDbContextMigrationTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteFixture fixture = new();

    public Task InitializeAsync() => this.fixture.InitializeAsync();

    public Task DisposeAsync() => this.fixture.DisposeAsync();

    public void Dispose() => this.fixture.Dispose();

    [Fact]
    public async Task MigrationApplies_CreatesAllFourTables()
    {
        await using var connection = new SqliteConnection(this.fixture.ConnectionString);
        await connection.OpenAsync();

        await using var cmd = connection.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";

        var tables = new List<string>();
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                tables.Add(reader.GetString(0));
            }
        }

        Assert.Contains("Matches", tables);
        Assert.Contains("Events", tables);
        Assert.Contains("MatchSnapshots", tables);
        Assert.Contains("EventParticipants", tables);
    }
}
