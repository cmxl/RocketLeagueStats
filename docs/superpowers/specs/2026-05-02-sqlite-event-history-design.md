# SQLite event history persistence — design

**Status:** approved (brainstorm), pending implementation plan
**Date:** 2026-05-02
**Scope:** Backend persistence only. Read endpoints, UI, retention, and analytics are out of scope.

## 1. Goal

Persist every `StatsEvent` flowing through `StatsEventBus` into a local SQLite database via Entity Framework Core, so that historic match data is queryable later. The first user-visible feature this unlocks is a "recap of past matches" page; future features include per-player aggregation across matches.

Optimisation priorities, in order:

1. **Save fast** — never block the listener thread; bursts up to ~50 events/second during play must be absorbed without drops attributable to the writer.
2. **Capture every event including periodic `UpdateState` snapshots** — full fidelity is required for forensic replay.
3. **Read can be slower** — the schema serves UI queries that fetch one match at a time; cross-match analytics is a future concern with room to add indexes/tables.
4. **Extensibility** — new event types from the wire must be storable without schema changes.

## 2. Replacing SQL Server

The current `StatsDbContext` is wired to `Microsoft.EntityFrameworkCore.SqlServer` with an `InMemory` fallback for tooling, and the schema is empty. Both packages are removed in this change; the transitive pin on `System.Security.Cryptography.Xml` (only present to mitigate a SQL-Server-package CVE) is removed too.

`Microsoft.EntityFrameworkCore.Sqlite` and `Microsoft.Data.Sqlite` are added in their place.

## 3. Architecture

The new persistence is a second `BackgroundService` subscriber on `StatsEventBus`, mirroring `JsonlEventLoggerService`. No changes to the listener, parser, or bus.

```
StatsApiClient → StatsEventParser → StatsEventBus
                                          │  (publish; drop-oldest if subscriber slow)
        ┌─────────────────────────────────┼──────────────────────────────────────┐
        ▼                                 ▼                                      ▼
  JsonlEventLoggerService     SqliteEventStoreService                 (future SignalR / hub)
  (opt-in, default off)       BackgroundService
                              single writer
                              batched transactions
                              drops own batch on failure
                                          │
                                          ▼
                                %LocalAppData%/RocketLeagueStats/stats.db
                                       (WAL mode)
```

### Isolation properties

- If SQLite stalls (slow disk, lock contention, etc.) only the writer's bounded channel fills and drops oldest events. Listener / JSONL / future SignalR are unaffected.
- If a batch insert throws (corrupt DB, disk full, constraint violation), we log + drop the batch + keep the service running. Matches `JsonlEventLoggerService`'s `IOException` handling.
- Bus-level drops surface via the existing `SubscriberDropTracker` 5-second coalesced warning log.

### Read path (future)

WebAPI endpoints will use `StatsDbContext` with `AsNoTracking()`. WAL mode means reads never block writes and vice versa.

## 4. Schema

Four tables. Types below describe the EF Core CLR → SQLite affinity mapping.

### 4.1 `Matches` — one row per `MatchGuid`

| Column | Type | Notes |
|---|---|---|
| `MatchGuid` | `TEXT PRIMARY KEY` | from `StatsEvent.MatchGuid` |
| `FirstSeenAtUtc` | `INTEGER NOT NULL` | unix-ms; first event observed for this guid |
| `CreatedAtUtc` | `INTEGER NULL` | from `MatchCreatedEvent.Timestamp` |
| `InitializedAtUtc` | `INTEGER NULL` | from `MatchInitializedEvent` |
| `EndedAtUtc` | `INTEGER NULL` | from `MatchEndedEvent` |
| `DestroyedAtUtc` | `INTEGER NULL` | from `MatchDestroyedEvent` |
| `WinnerTeamNum` | `INTEGER NULL` | from `MatchEndedEvent.WinnerTeamNum` |
| `EventCount` | `INTEGER NOT NULL DEFAULT 0` | running, updated per batch |
| `SnapshotCount` | `INTEGER NOT NULL DEFAULT 0` | running, updated per batch |
| `LastEventAtUtc` | `INTEGER NOT NULL` | latest timestamp seen |

**Index:** `IX_Matches_EndedAtUtc DESC` (recap list path).

The row is upserted lazily on first event sighting (`INSERT … ON CONFLICT(MatchGuid) DO UPDATE`) and progressively enriched as lifecycle events arrive.

### 4.2 `Events` — every discrete event (everything except `UpdateState`)

| Column | Type | Notes |
|---|---|---|
| `Id` | `INTEGER PRIMARY KEY` | rowid alias |
| `MatchGuid` | `TEXT NULL` | FK → `Matches(MatchGuid) ON DELETE CASCADE` (nullable: events occasionally arrive before MatchGuid is known) |
| `EventName` | `TEXT NOT NULL` | wire-name string; preserved as text so `UnknownDiscreteEvent` (forward-compat) needs no schema change |
| `TimestampUtc` | `INTEGER NOT NULL` | unix-ms |
| `Payload` | `TEXT NOT NULL` | JSON, serialised via existing `StatsEventJsonContext` (same shape JSONL writes today) |

**Indexes:**
- `IX_Events_MatchGuid_Id` — recap path (events of one match in arrival order; `Id` monotonic).
- `IX_Events_EventName_TimestampUtc` — analytics path ("all goals across the last month").

### 4.3 `MatchSnapshots` — `UpdateState` only

Separated from `Events` because of volume (~30Hz × full-match-state JSON dominates total bytes).

| Column | Type | Notes |
|---|---|---|
| `Id` | `INTEGER PRIMARY KEY` | rowid alias |
| `MatchGuid` | `TEXT NOT NULL` | FK → `Matches ON DELETE CASCADE` |
| `TimestampUtc` | `INTEGER NOT NULL` |  |
| `Payload` | `TEXT NOT NULL` | raw `MatchStateSnapshot.RawData` JSON |

**Index:** `IX_MatchSnapshots_MatchGuid_Id`. Stored as plain JSON text — no compression on the hot path.

### 4.4 `EventParticipants` — denormalised player lookup

Populated at write time by extracting `PlayerRef`s from typed events.

| Column | Type | Notes |
|---|---|---|
| `EventId` | `INTEGER NOT NULL` | FK → `Events(Id) ON DELETE CASCADE` |
| `MatchGuid` | `TEXT NOT NULL` | duplicated for query locality |
| `PlayerName` | `TEXT NOT NULL` | from `PlayerRef.Name` |
| `Shortcut` | `INTEGER NOT NULL` | from `PlayerRef.Shortcut` |
| `TeamNum` | `INTEGER NOT NULL` | from `PlayerRef.TeamNum` |
| `Role` | `TEXT NOT NULL` | one of: `Scorer`, `Assister`, `BallLastTouch`, `BallHit`, `MainTarget`, `SecondaryTarget` |
| `TimestampUtc` | `INTEGER NOT NULL` | duplicated from event for fast filtering without a join |

**Composite primary key:** `(EventId, PlayerName, Role)` — same player can appear once per role.

**Indexes:**
- `IX_EventParticipants_PlayerName_TimestampUtc DESC` — "all events involving Tobi, recent first."
- `IX_EventParticipants_MatchGuid_PlayerName` — per-match per-player rollups.

**Extraction rules:**

| Event | Roles emitted |
|---|---|
| `GoalScoredEvent` | `Scorer`, optional `Assister`, optional `BallLastTouch.Player` |
| `BallHitEvent` | every entry in `Players[]` as `BallHit` |
| `StatfeedEvent` | `MainTarget`, optional `SecondaryTarget` |
| `CrossbarHitEvent` | optional `BallLastTouch.Player` |

Future events with new `PlayerRef` fields are purely additive — extend the extractor only.

**Edge case — null `MatchGuid`:** `Events.MatchGuid` is nullable (some wire events arrive before a guid is known) but `EventParticipants.MatchGuid` is `NOT NULL`. When an event with `PlayerRef`s has a null `MatchGuid`, the event row is still inserted but participant rows are skipped (the per-player query path requires a match context anyway). In practice this is rare — discrete events with `PlayerRef`s always carry the guid in production traffic.

## 5. Write hot path

### 5.1 `SqliteEventStoreService : BackgroundService`

```
ExecuteAsync:
  reader = bus.Subscribe()
  buffer = List<StatsEvent>(capacity = MaxBatchSize)
  lastFlushAt = utcNow

  loop until cancelled:
    if WaitToReadAsync(timeout = MaxBatchLatency - elapsed):
      while reader.TryRead(out evt) and buffer.Count < MaxBatchSize:
        buffer.Add(evt)

    if buffer.Count >= MaxBatchSize OR (utcNow - lastFlushAt) >= MaxBatchLatency:
      flush(buffer)
      buffer.Clear()
      lastFlushAt = utcNow
```

Defaults (configurable):
- `MaxBatchSize = 200`
- `MaxBatchLatencyMs = 250`

This bounds write latency at ~250ms while keeping batches meaty (snapshots arrive at 30Hz = ~7 in 250ms; `BallHit` bursts fill 200 quickly).

### 5.2 Raw `Microsoft.Data.Sqlite` for writes

EF Core change tracking is bypassed on the hot path. Prepared `SqliteCommand` instances are created once at service start and reused per batch:

```
using var tx = connection.BeginTransaction();
upsertMatchAggregatesCommand.…ExecuteNonQuery();   // ON CONFLICT … DO UPDATE
foreach (evt in buffer):
  if evt is MatchStateSnapshot:
    insertSnapshotCommand.…ExecuteNonQuery();
  else:
    insertEventCommand.…ExecuteNonQuery();
    foreach (participant in extract(evt)):
      insertParticipantCommand.…ExecuteNonQuery();
tx.Commit();
```

EF Core still owns:
- the schema (migrations).
- the read-side `StatsDbContext` for future query endpoints.

### 5.3 Connection PRAGMAs (set once per writer connection)

```sql
PRAGMA journal_mode = WAL;        -- writers don't block readers
PRAGMA synchronous = NORMAL;      -- fsync once per checkpoint, not per commit; <1s loss on power failure (acceptable)
PRAGMA temp_store = MEMORY;
PRAGMA cache_size = -8000;        -- 8 MB page cache
PRAGMA foreign_keys = ON;
PRAGMA wal_autocheckpoint = 1000; -- checkpoint every ~1000 pages (~4 MB)
```

### 5.4 Failure handling

- **Batch insert throws** — log `LogWriteFailed`-equivalent, drop the batch, continue. Mirrors JSONL.
- **Bus channel back-pressure** — already handled by `StatsEventBus`; subscriber's bounded channel drops oldest; drops surface via `SubscriberDropTracker` coalesced warnings.
- **DB unreachable / corrupt at startup** — fail fast. `BackgroundServiceExceptionBehavior.StopHost` (already configured in `Program.cs`) tears the host down with the migration error visible.

### 5.5 Throughput sanity

Worst case during play: ~50 events/sec. With 250ms batches that is ~12 events/batch typical, up to 200 under burst. WAL-mode SQLite handles tens of thousands of inserts/sec on modest hardware; we are orders of magnitude under that. fsync occurs once per WAL checkpoint (~4MB written), not per commit, keeping commit latency low.

## 6. Configuration & DI

### 6.1 Package changes — `Directory.Packages.props`

```diff
- <PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.7" />
- <PackageVersion Include="Microsoft.EntityFrameworkCore.InMemory" Version="10.0.7" />
- <PackageVersion Include="System.Security.Cryptography.Xml" Version="10.0.7" />
+ <PackageVersion Include="Microsoft.EntityFrameworkCore.Sqlite" Version="10.0.7" />
+ <PackageVersion Include="Microsoft.Data.Sqlite" Version="10.0.7" />
```

Plus the matching `<PackageReference>` swap in `RocketLeagueStats.Core.csproj`.

### 6.2 Configuration section

```jsonc
// appsettings.json
{
  "EventLog": { "Enabled": false },          // flipped from true; JSONL is now opt-in
  "EventStore": {
    "Enabled": true,
    "MaxBatchSize": 200,
    "MaxBatchLatencyMs": 250
  },
  "ConnectionStrings": {
    "Stats": ""                              // empty → fall back to %LocalAppData%/RocketLeagueStats/stats.db
  }
}
```

`EventStoreOptions` lives in `RocketLeagueStats.Core/Configuration/`:

```csharp
public sealed class EventStoreOptions
{
    public const string SectionName = "EventStore";
    public bool Enabled { get; init; } = true;
    public int MaxBatchSize { get; init; } = 200;
    public int MaxBatchLatencyMs { get; init; } = 250;
}
```

### 6.3 Connection-string resolution

`Persistence/StatsConnectionString.cs`:

```csharp
public static class StatsConnectionString
{
    public static string Resolve(IConfiguration config)
    {
        var explicitConn = config.GetConnectionString("Stats");
        if (!string.IsNullOrWhiteSpace(explicitConn))
            return explicitConn;

        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "RocketLeagueStats");
        Directory.CreateDirectory(dir);
        return $"Data Source={Path.Combine(dir, "stats.db")}";
    }
}
```

Resolved once at DI registration; both `DbContext` and the raw-ADO writer consume the same string. The raw-ADO writer takes it by injecting a typed wrapper:

```csharp
public sealed record EventStoreConnectionString(string Value);
```

(plain `string` would conflict with other DI registrations — the wrapper makes the dependency explicit.)

### 6.4 `AddRocketLeagueStatsCore` diff

```diff
-services.AddDbContext<StatsDbContext>((sp, opts) =>
-{
-    var connection = configuration.GetConnectionString("Stats");
-    if (!string.IsNullOrWhiteSpace(connection))
-        opts.UseSqlServer(connection);
-    else
-        opts.UseInMemoryDatabase("RocketLeagueStats-Disabled");
-});
+services.Configure<EventStoreOptions>(configuration.GetSection(EventStoreOptions.SectionName));
+
+var connectionString = StatsConnectionString.Resolve(configuration);
+services.AddSingleton(new EventStoreConnectionString(connectionString));     // for the raw-ADO writer
+services.AddDbContext<StatsDbContext>(opts => opts.UseSqlite(connectionString));
```

### 6.5 `AddRocketLeagueStatsHostingDefaults` diff

```diff
 services.AddHostedService<IniBootstrapHostedService>();
 services.AddHostedService<StatsApiListenerService>();
 services.AddHostedService<JsonlEventLoggerService>();
 services.AddHostedService<SnapshotDumperService>();
+services.AddHostedService<EventStoreStartupService>();
+services.AddHostedService<SqliteEventStoreService>();
```

`EventStoreStartupService` runs first (short-lived `IHostedService`) and:

1. Calls `await dbContext.Database.MigrateAsync()` (creates schema on fresh install, applies pending migrations on upgrade).
2. Reads the resolved DB path and `FileInfo(path).Length`.
3. Counts `Matches` rows.
4. Emits a single `LogInformation` via `LoggerMessage.Define` (same pattern as the other services):

   ```
   [INF] Event store ready — path: C:\Users\thcmx\AppData\Local\RocketLeagueStats\stats.db, size: 142.3 MB, matches: 17
   ```

If the migration fails the service throws and `BackgroundServiceExceptionBehavior.StopHost` already configured in `Program.cs` tears the host down with the error visible.

## 7. Migrations

This deviates from the global `CLAUDE.md` rule "code-first approach (no migrations)": that rule is written for a SQL-Server-first workflow built around `.sqlproj` schema sources + Docker SQL Server. None of that applies to a single-user SQLite file the app fully owns. EF Core migrations are the right tool for this case.

### 7.1 Tooling

`Microsoft.EntityFrameworkCore.Design` is already a `PrivateAssets="all"` reference. Add `dotnet-ef` as a local tool:

```json
// .config/dotnet-tools.json (new file at repo root)
{
  "version": 1,
  "isRoot": true,
  "tools": {
    "dotnet-ef": { "version": "10.0.7", "commands": ["dotnet-ef"] }
  }
}
```

The existing `StatsDbContextDesignTimeFactory` flips from `UseInMemoryDatabase` to `UseSqlite("Data Source=design.db")` so `dotnet ef migrations add` works locally.

### 7.2 Initial migration

One migration `InitialEventStoreSchema` creating all four tables and their indexes per Section 4. Migrations live in `src/RocketLeagueStats.Core/Persistence/Migrations/`.

### 7.3 Migration helper script

`tools/Migrate-StatsDb.ps1`:

```powershell
param([Parameter(Mandatory=$true)][string]$Name)
dotnet ef migrations add $Name `
    --project ./src/RocketLeagueStats.Core `
    --startup-project ./src/RocketLeagueStats.WebApi `
    --output-dir Persistence/Migrations
```

### 7.4 `StatsDbContext` rebuild

The current placeholder is replaced with `DbSet<Match>`, `DbSet<EventRecord>`, `DbSet<MatchSnapshotRecord>`, `DbSet<EventParticipant>`, plus `OnModelCreating` configuring all indexes and FKs. EF entity classes live in `src/RocketLeagueStats.Core/Persistence/Entities/`.

## 8. Tests

### 8.1 `RocketLeagueStats.Core.Tests` — in-process, real SQLite

A new `SqliteFixture : IAsyncLifetime` creates a fresh temp `Data Source=test-{guid}.db` and runs migrations in `InitializeAsync`; deletes the file in `DisposeAsync`. No mocking the database, no Docker.

Tests:

| Test | What it proves |
|---|---|
| `WritesGoalScored_PersistsEventAndParticipants` | A `GoalScoredEvent` round-trips with rows in `Events` and `EventParticipants` (`Scorer`/`Assister`) |
| `WritesBallHit_PersistsAllPlayersAsParticipants` | `BallHitEvent` with N players → N `EventParticipants` rows with role `BallHit` |
| `WritesUpdateState_GoesToMatchSnapshotsTable` | `MatchStateSnapshot` lands in `MatchSnapshots`, not `Events` |
| `MatchRow_UpsertedOnFirstEvent_EnrichedOnLifecycleEvents` | First sighting creates `Matches` row; `MatchEndedEvent` populates `EndedAtUtc` and `WinnerTeamNum` |
| `Batching_FlushesAtMaxBatchSize` | After N events with no time elapsed, single batch commit; row count + `LastEventAtUtc` verified |
| `Batching_FlushesAtMaxBatchLatency` | Few events but latency exceeded → still flushes |
| `BatchInsertFailure_LogsAndContinues` | Force a constraint violation → service stays alive, next batch succeeds |
| `BusBackpressure_DropsAreReportedNotThrown` | Flood the bus → no exceptions, drop warnings observed |
| `MigrationApplies_OnFreshDatabase` | `Database.MigrateAsync()` against empty file creates all four tables |
| `Idempotency_ReplayingSameEventsIsSafe` | Inserting events twice doesn't violate unique constraints |

The bus is real (singleton); the writer service is constructed manually with stub `IOptions<EventStoreOptions>`. Tests publish via `bus.Publish(…)` and wait on a `TaskCompletionSource` that completes when expected row counts hit the DB.

### 8.2 `RocketLeagueStats.WebApi.Tests` — host integration

Add one test using `WebApplicationFactory<Program>`:

| Test | What it proves |
|---|---|
| `Host_StartsWithFreshDatabase_LogsPathAndSize` | Boot WebApi with a temp connection string; `EventStoreStartupService` logs the expected info; both startup and writer services run |

`ConfigureAppConfiguration` injects `ConnectionStrings:Stats` pointing at a temp file; teardown deletes it.

### 8.3 Manual smoke (documented, not automated)

Run a real Rocket League match against the listener and verify:

- `stats.db` exists at the resolved path.
- Startup log line shows correct path and size.
- `Matches` row exists with non-null `EndedAtUtc` and `WinnerTeamNum`.
- `Events` count is in low thousands (order-of-magnitude check).
- `MatchSnapshots` count ≈ `match_duration_seconds × 30`.

### 8.4 Not tested

- SQLite engine itself (tested upstream).
- EF Core migration apply logic.
- Cross-platform path resolution beyond Windows.
- Performance benchmarks. Add a `BenchmarkDotNet` project later only if real-world play surfaces a problem.

## 9. Out of scope

- WebAPI read endpoints (`GET /api/matches`, `GET /api/matches/{guid}/events`, …) — separate spec.
- Angular match-list / recap UI — separate spec.
- Per-player analytics endpoints / queries — future, supported by `EventParticipants` schema.
- Retention / eviction policy — never auto-delete by design; user manages disk usage.
- Snapshot compression — accepted ~50–100MB/match cost.
- `BenchmarkDotNet` performance harness.
- SignalR live-broadcast subscriber (orthogonal future bus subscriber).
- Cross-platform paths beyond Windows.
- Removing the `--no-log` CLI flag (left wired; just flips the default of `EventLog:Enabled` to `false`).

## 10. Open question — none

All design questions surfaced during brainstorming have been resolved with explicit answers:

- **Schema strategy:** envelope + side-tables (Approach 2 of three options).
- **Snapshot capture:** every `UpdateState` saved (option D).
- **JSONL future:** kept, default off (option B).
- **DB location:** `ConnectionStrings:Stats` configurable, fallback to `%LocalAppData%/RocketLeagueStats/stats.db`.
- **Retention:** never auto-delete.
- **Startup feedback:** log path + file size + match count.
- **Hot-path write tech:** raw `Microsoft.Data.Sqlite` (not EF) inside one transaction per batch.
- **Defaults:** batch size 200, latency 250ms, `synchronous=NORMAL`.
- **Migrations:** EF Core migrations, deviating from global `CLAUDE.md`'s SQL-Server-first "no migrations" rule.
