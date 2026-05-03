namespace RocketLeagueStats.WebApi.Mediator.Handlers;

using global::Mediator;
using Microsoft.EntityFrameworkCore;
using RocketLeagueStats.Core.Persistence;
using RocketLeagueStats.WebApi.Mediator.Queries;

internal sealed class DeleteMatchHandler(StatsDbContext db)
    : ICommandHandler<DeleteMatchCommand, bool>
{
    // Re-capture the primary-constructor parameter so this.db.* works under IDE0009 (instance
    // qualification on field access).
    private readonly StatsDbContext db = db;

    public async ValueTask<bool> Handle(DeleteMatchCommand cmd, CancellationToken ct)
    {
        // ExecuteDeleteAsync issues a single DELETE statement and bypasses change tracking.
        // SQLite's foreign-key cascades (configured on Events / MatchSnapshots / EventParticipants
        // / PlayerMatchStats with OnDelete(Cascade) in StatsDbContext.OnModelCreating) handle the
        // child rows in the same statement — so this one call cleans up every related row.
        // EF Core's SQLite provider enables PRAGMA foreign_keys = ON for managed connections,
        // which is required for the cascade to fire.
        var rowsAffected = await this.db.Matches
            .Where(m => m.MatchGuid == cmd.MatchId)
            .ExecuteDeleteAsync(ct);

        return rowsAffected > 0;
    }
}
