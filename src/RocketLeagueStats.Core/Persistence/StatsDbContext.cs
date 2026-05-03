namespace RocketLeagueStats.Core.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RocketLeagueStats.Core.Persistence.Entities;

public sealed class StatsDbContext(DbContextOptions<StatsDbContext> options) : DbContext(options)
{
    public DbSet<Match> Matches => this.Set<Match>();

    public DbSet<EventRecord> Events => this.Set<EventRecord>();

    public DbSet<MatchSnapshotRecord> MatchSnapshots => this.Set<MatchSnapshotRecord>();

    public DbSet<EventParticipant> EventParticipants => this.Set<EventParticipant>();

    public DbSet<PlayerMatchStats> PlayerMatchStats => this.Set<PlayerMatchStats>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Match>(b =>
        {
            b.ToTable("Matches");
            b.HasKey(x => x.MatchGuid);
            b.Property(x => x.MatchGuid).HasMaxLength(64);
            // Color values are 6-digit hex without leading '#' (10 chars is generous slack for
            // sponsor-branded matches that might use longer descriptors). Names are short.
            b.Property(x => x.BlueTeamName).HasMaxLength(64);
            b.Property(x => x.BlueColorPrimary).HasMaxLength(16);
            b.Property(x => x.BlueColorSecondary).HasMaxLength(16);
            b.Property(x => x.OrangeTeamName).HasMaxLength(64);
            b.Property(x => x.OrangeColorPrimary).HasMaxLength(16);
            b.Property(x => x.OrangeColorSecondary).HasMaxLength(16);
            b.Property(x => x.Arena).HasMaxLength(64);
            b.HasIndex(x => x.EndedAtUtc)
                .IsDescending()
                .HasDatabaseName("IX_Matches_EndedAtUtc");
        });

        modelBuilder.Entity<EventRecord>(b =>
        {
            b.ToTable("Events");
            b.HasKey(x => x.Id);
            b.Property(x => x.MatchGuid).HasMaxLength(64);
            b.Property(x => x.EventName).HasMaxLength(64).IsRequired();
            b.Property(x => x.Payload).IsRequired();
            b.HasIndex(x => new { x.MatchGuid, x.Id })
                .HasDatabaseName("IX_Events_MatchGuid_Id");
            b.HasIndex(x => new { x.EventName, x.TimestampUtc })
                .HasDatabaseName("IX_Events_EventName_TimestampUtc");
            // IsRequired(false) is explicit because EF would otherwise infer "required" from the
            // string property type and emit NOT NULL on the FK column; we need the column nullable
            // so events that arrive before a MatchGuid is known can still be persisted.
            b.HasOne<Match>()
                .WithMany()
                .HasForeignKey(x => x.MatchGuid)
                .HasPrincipalKey(m => m.MatchGuid)
                .OnDelete(DeleteBehavior.Cascade)
                .IsRequired(false);
        });

        modelBuilder.Entity<MatchSnapshotRecord>(b =>
        {
            b.ToTable("MatchSnapshots");
            b.HasKey(x => x.Id);
            b.Property(x => x.MatchGuid).HasMaxLength(64).IsRequired();
            b.Property(x => x.Payload).IsRequired();
            b.HasIndex(x => new { x.MatchGuid, x.Id })
                .HasDatabaseName("IX_MatchSnapshots_MatchGuid_Id");
            b.HasOne<Match>()
                .WithMany()
                .HasForeignKey(x => x.MatchGuid)
                .HasPrincipalKey(m => m.MatchGuid)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EventParticipant>(b =>
        {
            b.ToTable("EventParticipants");
            b.HasKey(x => new { x.EventId, x.PlayerName, x.Role });
            b.Property(x => x.MatchGuid).HasMaxLength(64).IsRequired();
            b.Property(x => x.PlayerName).HasMaxLength(128).IsRequired();
            b.Property(x => x.Role).HasMaxLength(32).IsRequired();
            // IsDescending(false, true) means PlayerName ASC, TimestampUtc DESC — recent events for a
            // player should be the cheap end of the index walk for "last 30 days for X" queries.
            b.HasIndex(x => new { x.PlayerName, x.TimestampUtc })
                .IsDescending(false, true)
                .HasDatabaseName("IX_EventParticipants_PlayerName_TimestampUtc");
            b.HasIndex(x => new { x.MatchGuid, x.PlayerName })
                .HasDatabaseName("IX_EventParticipants_MatchGuid_PlayerName");
            b.HasOne<EventRecord>()
                .WithMany()
                .HasForeignKey(x => x.EventId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PlayerMatchStats>(b =>
        {
            b.ToTable("PlayerMatchStats");
            // Composite PK on (MatchGuid, Shortcut) — Shortcut is RL's stable per-player id within
            // a match. Cascade-delete from Match so removing a match drops its per-player rows too.
            b.HasKey(x => new { x.MatchGuid, x.Shortcut });
            b.Property(x => x.MatchGuid).HasMaxLength(64).IsRequired();
            b.Property(x => x.PlayerName).HasMaxLength(128).IsRequired();
            b.Property(x => x.Platform).HasMaxLength(32).IsRequired();
            b.HasIndex(x => x.PlayerName)
                .HasDatabaseName("IX_PlayerMatchStats_PlayerName");
            b.HasOne<Match>()
                .WithMany()
                .HasForeignKey(x => x.MatchGuid)
                .HasPrincipalKey(m => m.MatchGuid)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
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
