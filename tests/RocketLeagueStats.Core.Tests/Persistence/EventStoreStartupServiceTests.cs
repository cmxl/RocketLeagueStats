namespace RocketLeagueStats.Core.Tests.Persistence;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using RocketLeagueStats.Core.HostedServices;
using RocketLeagueStats.Core.Persistence;

public sealed class EventStoreStartupServiceTests : IAsyncLifetime, IDisposable
{
    private readonly SqliteFixture fixture = new();

    // The service runs migrations itself; we don't pre-migrate via the fixture.
    public Task InitializeAsync() => Task.CompletedTask;

    public Task DisposeAsync() => this.fixture.DisposeAsync();

    public void Dispose() => this.fixture.Dispose();

    [Fact]
    public async Task StartAsync_AppliesMigrationsAndLogsPathAndSize()
    {
        var logger = Substitute.For<ILogger<EventStoreStartupService>>();

        // LoggerMessage.Define calls IsEnabled before Log; NSubstitute returns false by default,
        // which causes the log call to be short-circuited. Enable all levels so Log is reached.
        logger.IsEnabled(Arg.Any<LogLevel>()).Returns(true);

        // Build a scope factory that dispenses the real StatsDbContext from the fixture.
        var dbContext = this.fixture.CreateDbContext();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(StatsDbContext)).Returns(dbContext);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var asyncScope = Substitute.For<IAsyncDisposable>();

        // IServiceScopeFactory.CreateAsyncScope() is an extension method that delegates to
        // CreateScope(); stub the underlying interface method so the extension resolves correctly.
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var service = new EventStoreStartupService(
            new EventStoreConnectionString(this.fixture.ConnectionString),
            scopeFactory,
            logger);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        dbContext.Dispose();

        Assert.True(File.Exists(this.fixture.FilePath), "DB file should exist after migration.");

        // LoggerMessage.Define produces a state object whose ToString() returns the formatted message.
        var captured = logger.ReceivedCalls()
            .Where(c => c.GetMethodInfo().Name == "Log")
            .Select(c => c.GetArguments())
            .Where(args => args.Length > 0 && args[0] is LogLevel level && level == LogLevel.Information)
            .Select(args => args[2]?.ToString() ?? string.Empty)
            .ToList();

        Assert.Contains(captured, msg =>
            msg.Contains("Event store ready") &&
            msg.Contains(this.fixture.FilePath) &&
            msg.Contains("size") &&
            msg.Contains("matches"));
    }
}
