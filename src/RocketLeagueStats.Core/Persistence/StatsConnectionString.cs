namespace RocketLeagueStats.Core.Persistence;

using Microsoft.Extensions.Configuration;

public static class StatsConnectionString
{
    public const string ConnectionStringName = "Stats";
    public const string LocalAppDataDirectoryName = "RocketLeagueStats";
    public const string DefaultDatabaseFileName = "stats.db";

    public static string Resolve(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var explicitConn = configuration.GetConnectionString(ConnectionStringName);
        if (!string.IsNullOrWhiteSpace(explicitConn))
        {
            return explicitConn;
        }

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            LocalAppDataDirectoryName);
        Directory.CreateDirectory(dir);

        return $"Data Source={Path.Combine(dir, DefaultDatabaseFileName)}";
    }

    public static string ExtractDataSourcePath(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        var builder = new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(connectionString);
        return builder.DataSource;
    }
}
