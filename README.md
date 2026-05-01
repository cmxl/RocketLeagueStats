# RocketLeagueStats

A live, in-terminal feed of every event your Rocket League match emits — goals, ball hits, demolitions, saves, the clock, the whole match lifecycle — captured straight from the official **Rocket League Stats API** (released April 2026).

> **Status:** v1 — command-line tool only. A Discord broadcaster and a web dashboard are planned but not built yet.

```text
14:02:11 Match created
14:02:14 Match initialized
14:02:17 Countdown begin
14:02:20 Round started
14:02:33 BallHit — Stinkmaster · 412 → 1981 UU/s
14:02:34 Save — Stinkmaster
14:02:41 GOAL — Hellcat (assist: Stinkmaster) · 2104 UU/s (Blue)
14:02:43 Goal replay start
14:02:48 Goal replay end
...
14:07:12 Match ended — winner: Blue
```

(In your terminal it's colour-coded — goals in yellow, demos in red, replay markers in magenta, teams in blue/orange.)

---

## What it does

The tool opens the local TCP socket Rocket League exposes for stats (port `49123` by default), parses every event it receives, and prints a one-line summary in your terminal. It also writes a JSONL file under `logs/` so you can replay or analyse a session later.

It can capture all 19 events documented in the official Stats API plus three undocumented replay markers seen on the wire:

| Category | Events |
|---|---|
| Discrete game events | `BallHit`, `CrossbarHit`, `GoalScored`, `StatfeedEvent` (saves, demos, epic saves, etc.) |
| Match lifecycle | `MatchCreated`, `MatchInitialized`, `MatchEnded`, `MatchPaused`, `MatchUnpaused`, `MatchDestroyed` |
| Round / clock | `CountdownBegin`, `RoundStarted`, `ClockUpdatedSeconds` |
| Replay | `GoalReplayStart`, `GoalReplayWillEnd`, `GoalReplayEnd`, `ReplayCreated`, `ReplayPlaybackStart`, `ReplayWillEnd`, `ReplayPlaybackEnd` |
| Podium | `PodiumStart` |

---

## Requirements

- **Windows 10 / 11** — the Stats API only runs where Rocket League runs, and Rocket League is Windows-only.
- **Rocket League** installed via Steam or Epic Games.
- **.NET 10 SDK** — get it from <https://dotnet.microsoft.com/download>. Verify with `dotnet --version` (should report `10.0.x`).

You don't need Docker, a database, or any other external service to use the tool. (There's a Docker Compose file in `tools/` reserved for future aggregation work — ignore it.)

---

## Quick start

1. Clone this repo and open a terminal in the project root:
   ```powershell
   git clone https://github.com/cmxl/RocketLeagueStats.git
   cd RocketLeagueStats
   ```

2. **Close Rocket League** if it's running. (The tool needs to write a small config file inside the game's install folder; it refuses to do this while the game is open.)

3. Run the tool once — this will detect your Rocket League install (Steam or Epic) and enable the Stats API by writing two lines into `DefaultStatsAPI.ini`:
   ```powershell
   dotnet run --project src/RocketLeagueStats.Console
   ```
   You should see a log line like `Set [TAGame.MatchStatsExporter_TA] Port in C:\...\DefaultStatsAPI.ini`. The tool then listens on port `49123`.

4. **Start Rocket League and enter a match.** Events should start flowing into your terminal within a few seconds.

That's it. To stop, press `Ctrl+C`.

> **Already had a `DefaultStatsAPI.ini`?** The original is backed up alongside it as `DefaultStatsAPI.ini.bak.YYYYMMDD` before the first write each day, so you can restore it if you ever want to.

---

## CLI flags

| Flag | Effect |
|---|---|
| `--port <n>` | Override the TCP port (default `49123`). Match this with `Port=` in `DefaultStatsAPI.ini` if you change it. |
| `--raw` | Print every received line as `<<< <raw>` with no parsing or colours. Useful for debugging or for piping somewhere else. |
| `--no-log` | Turn off the JSONL event log entirely. |
| `--no-config-helper` | Skip the auto-write of `DefaultStatsAPI.ini`. Use this if you want to manage the ini yourself. |
| `--trace` | Diagnostic mode — dumps every raw socket chunk (length, hex, UTF-8 preview). **Events are not parsed or published in this mode.** Only useful if you're investigating wire-format issues. |

Examples:

```powershell
# Use a non-default port
dotnet run --project src/RocketLeagueStats.Console -- --port 49124

# Skip the JSONL log, just watch the terminal
dotnet run --project src/RocketLeagueStats.Console -- --no-log
```

The `--` separator is required: it tells `dotnet run` "everything after this goes to the app, not to me".

---

## Where things land on disk

| What | Where (defaults) |
|---|---|
| Event log (JSONL, one event per line) | `<cwd>/logs/rl-stats-YYYY-MM-DD.jsonl` |
| Application log (rolling daily) | `<cwd>/logs/app-YYYYMMDD.log` |
| Game-side config the tool maintains | `<RocketLeague install>/TAGame/Config/DefaultStatsAPI.ini` |

Both log directories are kept for 7 days by default (the older ones are auto-deleted).

---

## Configuration

All defaults live in `src/RocketLeagueStats.Console/appsettings.json`. You can override settings via:

- **Environment variables** — `ROCKETLEAGUESTATS__STATSAPI__PORT=49124` (note the double underscores)
- **CLI flags** — see the table above
- **Editing `appsettings.json` directly**

Override precedence is `appsettings.json` → environment → CLI (right-most wins).

The most useful keys:

```jsonc
{
  "StatsApi": { "Port": 49123 },
  "GameSetup": {
    "AutoConfigureIni": true,    // set false to manage the ini yourself
    "PacketSendRate": 30          // 30 packets/sec is the documented default
  },
  "EventLog": {
    "Enabled": true,
    "RetentionDays": 7,
    "MaxFileSizeBytes": 104857600  // 100 MB cap per day
  }
}
```

---

## Troubleshooting

**"No events appear after I start a match."**
Check that `DefaultStatsAPI.ini` in `<RocketLeague>\TAGame\Config\` contains `Port=49123` and `PacketSendRate=30` under `[TAGame.MatchStatsExporter_TA]`. If you've used `--port` to change the port, update the ini to match.

**"Rocket League is running; close the game and retry."**
The auto-config helper refuses to overwrite the ini while the game has the file open. Close Rocket League, run the tool once to bootstrap the ini, then start the game.

**The tool found my install in the wrong place / didn't find it at all.**
Pass `--no-config-helper` and edit `DefaultStatsAPI.ini` manually. The two lines you need are:

```ini
[TAGame.MatchStatsExporter_TA]
PacketSendRate=30
Port=49123
```

**Events look garbled or I see "unknown:" lines.**
Run with `--trace` and capture a few seconds of output. Open an issue with the trace attached — it usually means the wire format has shifted.

---

## For developers

### Project layout

```
src/
  RocketLeagueStats.Core/        domain, parsing, event bus, install detection, ini writer
  RocketLeagueStats.Console/     generic-host app with hosted services
tests/
  RocketLeagueStats.Core.Tests/  xUnit + NSubstitute, includes a real captured-session JSONL replay
tools/
  docker-compose.yml             SQL Server 2022, reserved for future aggregation
docs/                            specs & plans (currently empty in v1)
```

### Build and test

```powershell
dotnet build                       # warnings-as-errors
dotnet test                        # all 26 tests should pass
```

### Manual smoke test

```powershell
dotnet run --project src/RocketLeagueStats.Console
# Start Rocket League, enter a match — events appear in the terminal.
```

### Tech notes

- Generic Host with four hosted services: ini bootstrap, TCP listener, console renderer, JSONL logger
- Event bus is an internal `Channel<StatsEvent>` — single producer (the listener), multiple consumers
- Rendering is `Spectre.Console` for colour markup
- Logging is Serilog (console + rolling daily file)
- JSON is `System.Text.Json` with a source-generated context — no reflection at runtime

Roadmap items kept out of v1 deliberately: Discord broadcasting and an Angular web frontend each have their own specs.

---

## License

[MIT](LICENSE) — © 2026 cmxl
