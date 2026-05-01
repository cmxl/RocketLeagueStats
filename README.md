# RocketLeagueStats

A live, in-terminal feed of every event your Rocket League match emits — goals, ball hits, demolitions, saves, the clock, the whole match lifecycle — captured straight from the official **Rocket League Stats API** (released April 2026).

> **Status:** v1.5 — console tool + web dashboard. A Discord broadcaster is planned but not built yet.

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
  RocketLeagueStats.Core/        domain, parsing, event bus, install detection, ini writer, shared hosted services
  RocketLeagueStats.Console/     v1 terminal-only host (Microsoft.NET.Sdk, plain generic host)
  RocketLeagueStats.WebApi/      web dashboard host: SignalR hub, Minimal API, projectors, wwwroot
  RocketLeagueStats.WebApp/      Angular 21 SPA (builds into WebApi/wwwroot via Build-WebApp.ps1)
tests/
  RocketLeagueStats.Core.Tests/  xUnit + NSubstitute; includes captured-session JSONL replay
  RocketLeagueStats.WebApi.Tests/ integration tests for API endpoints and hub
  RocketLeagueStats.WebApp.E2E/  Playwright smoke specs (require running EXE on :5000)
tools/
  Build-WebApp.ps1               Build Angular -> deploy to WebApi/wwwroot
  Build-Release.ps1              Console release pipeline: tests -> publish -> zip
  Build-Release-WebApi.ps1       Web Dashboard release pipeline: tests -> Angular -> publish -> zip
  docker-compose.yml             SQL Server 2022, reserved for future aggregation
docs/
  architecture.md                Mermaid diagrams and event-flow tables
  api-contract.md                REST + SignalR hub reference with example payloads
```

### Build and test

```powershell
dotnet build                       # warnings-as-errors
dotnet test                        # 64 Core + 57 WebApi.Tests = 121 tests
cd src/RocketLeagueStats.WebApp
npm test                           # 9 Vitest specs (pipes, app smoke)
```

### Manual smoke test

```powershell
# Console (legacy terminal feed)
dotnet run --project src/RocketLeagueStats.Console

# WebApi (dashboard host) — open http://localhost:5000/ in a browser
dotnet run --project src/RocketLeagueStats.WebApi -- --no-config-helper --no-log
```

### Tech notes

- **Console** uses `Host.CreateApplicationBuilder` (generic host); **WebApi** uses `WebApplication.CreateBuilder` (web host). Both register the same shared hosted services from Core: ini bootstrap, TCP listener, JSONL logger.
- Console adds `ConsoleRendererService` (Spectre terminal markup); WebApi adds `LiveMatchProjector` (translates bus events into SignalR broadcasts + history index updates).
- Event bus is `RocketLeagueStats.Core.Bus.StatsEventBus` — a `Channel<StatsEvent>` with multi-subscriber fan-out. Single producer (the listener), N consumers.
- WebApi's REST + SignalR JSON uses `System.Text.Json` with `JsonNamingPolicy.CamelCase` for both property names and enum values. TypeScript client types match exactly.
- Logging is Serilog (console + rolling daily file).

Roadmap items kept out of v1 deliberately: Discord broadcasting has its own spec.

---

## Two runnable projects

After v1.5, the project ships two independent executables:

- **`RocketLeagueStats.exe`** (Console) — the original v1 terminal feed. Connects to RL's TCP API, prints colour-coded events to terminal, writes JSONL logs. Run as `dotnet run --project src/RocketLeagueStats.Console`.
- **`RocketLeagueStats.WebApi.exe`** (Web Dashboard) — opens the same TCP connection, exposes a SignalR hub at `/hub/stats`, REST API at `/api/*`, and serves the Angular SPA from `wwwroot/`. Run as `dotnet run --project src/RocketLeagueStats.WebApi`. Open `http://localhost:5000/` in a browser.

Only one can run at a time per game session — Rocket League's Stats API allows a single TCP listener.

---

## Web Dashboard (v1.5)

Open `http://localhost:5000/` in any browser on the same machine — or `http://<gaming-pc>:5000/` from any device on your home LAN — while the EXE is running. The dashboard provides a live match scoreboard, a history of completed matches, and a per-match recap with charts.

### Configuration

The WebApi project supports the same CLI flags as the Console plus a port override:

| CLI flag | Config key | Default | Description |
|---|---|---|---|
| `--port <n>` | `StatsApi:Port` | `49123` | RL Stats API TCP port the listener binds to |
| `--web-port <n>` | `Web:Port` | `5000` | HTTP port the dashboard listens on |
| `--no-config-helper` | `GameSetup:AutoConfigureIni=false` | enabled | Skip auto-writing RL's `DefaultStatsAPI.ini` |
| `--no-log` | `EventLog:Enabled=false` | enabled | Don't write JSONL event logs |
| `--trace` | `StatsApi:TraceMode=true` | off | Diagnostic raw-socket dump |
| `--dump-snapshot` | `Diagnostics:DumpSnapshots=true` | off | Write the first `MatchStateSnapshot` of each match to `logs/snapshots/snapshot-<timestamp>-match<N>.json` for wire-format inspection |

The dashboard binds to `0.0.0.0` by default — anything on your LAN can reach it at `http://<gaming-pc>:5000/`. If you want localhost-only, set `Web:Url` to `http://127.0.0.1:5000` in `appsettings.json`.

> The legacy console-only mode is `RocketLeagueStats.Console` itself — there's no `--no-web` flag because the two hosts are independent projects. Run whichever fits your need.

### Features (v1)

- **Live match view** with RLCS-style scoreboard, action feed (goals, saves, demos, epic saves), per-player tallies, time-since-last-goal counter, and cinematic goal lower-thirds
- **Match history** with filtering (Online / Casual / Tournament / Private; toggle to include training/free play)
- **Recap view** for any completed match: final score hero, MVP card, goal timeline, time-between-goals chart, per-player stats, speed leaderboard, cumulative-score game-flow chart
- **Settings page** to configure your in-game name (powers own-card highlight in live view)

### Settings persistence

User settings persist at `%APPDATA%/RocketLeagueStats/settings.json`. Edit by hand or via the dashboard's `/settings` page.

### Tech stack

- Backend: .NET 10 + ASP.NET Core, SignalR, `martinothamar/Mediator`, Serilog
- Frontend: Angular 21 (zoneless, signals), NgRx SignalStore, Tailwind v4, Apache ECharts, `@microsoft/signalr`
- Tests: xUnit + NSubstitute (.NET), Vitest (Angular), Playwright (E2E)

### Building the dashboard for release

The dashboard is a single Windows executable that bundles the .NET host *and* the Angular SPA (the bundle ships inside the EXE's `wwwroot`). Two build modes:

```powershell
# Small EXE (~6 MB zip) — requires .NET 10 runtime installed on the target
pwsh ./tools/Build-Release-WebApi.ps1 -Version 1.5.0

# Self-contained EXE (~48 MB compressed zip) — runs on any Windows machine, no prereqs
pwsh ./tools/Build-Release-WebApi.ps1 -Version 1.5.0 -SelfContained
```

What the script does:
1. Runs the full test suite (skip with `-SkipTests` for faster iteration)
2. Builds the Angular bundle via `Build-WebApp.ps1` (npm ci + ng build production)
3. Copies the bundle into `src/RocketLeagueStats.WebApi/wwwroot/`
4. `dotnet publish` the WebApi as a single-file `win-x64` executable
5. Zips the publish folder, generates a SHA256 checksum, prints the upload command

Artifacts land in `artifacts/RocketLeagueStats-WebApi-v<version>-win-x64[-self-contained].zip`. Unzip and run `RocketLeagueStats.WebApi.exe` — open `http://localhost:5000/` in a browser.

To build just the Angular bundle (for local production-like testing) without packaging:

```powershell
pwsh ./tools/Build-WebApp.ps1
dotnet run --project src/RocketLeagueStats.WebApi -- --no-config-helper
# Open http://localhost:5000/
```

For active Angular development with hot reload, use the dev proxy (two terminals):

```powershell
# Terminal 1 — API on :5000
dotnet run --project src/RocketLeagueStats.WebApi -- --no-config-helper --no-log

# Terminal 2 — ng serve on :4200, proxies /api/* and /hub/* to :5000
cd src/RocketLeagueStats.WebApp
npm start
# Open http://localhost:4200/
```

The legacy console release is unchanged — `pwsh ./tools/Build-Release.ps1 -Version 1.0.x` produces a small console-only EXE.

---

## License

[MIT](LICENSE) — © 2026 cmxl
