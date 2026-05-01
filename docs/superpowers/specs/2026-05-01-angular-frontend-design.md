# RocketLeagueStats — Angular Web Dashboard (v1) Design

**Date:** 2026-05-01
**Status:** Approved (awaiting spec self-review)
**Scope:** v1 Angular web dashboard — live match view, post-match recap, history list, settings.
**Supersedes:** None — first web-tier spec for this project.
**Builds on:** [v1 console app](../../../memory/project_v1_console_app_state.md) (completed; provides bus + TCP listener + JSONL persistence)

---

## 1. Summary

Add a web dashboard to RocketLeagueStats that complements the existing console app. The dashboard provides:

- A **live match view** with RLCS-broadcast styling (live scoreboard, action feed, per-player tallies, time-since-last-goal counter, cinematic goal lower-thirds)
- A **post-match recap view** with TV-broadcast structure (final score hero, MVP card, goal timeline, time-between-goals chart, per-player stats table, speed leaderboard, cumulative-score game-flow chart)
- A **history view** of completed matches with type filtering (default: Online only; toggle for training/free play)
- A **settings page** to configure the user's in-game name (powers own-card highlight + win/loss tagging)
- A **landing page** that lets the user choose between live or recaps — no auto-routing

The dashboard runs as part of the existing `RocketLeagueStats.Console.exe` process: a single self-contained Windows EXE that owns the TCP connection to Rocket League's Stats API, the in-process event bus, the JSONL log, the SignalR hub, the REST API, and the Angular static files.

## 2. Goals

- Provide a TV-broadcast-quality visual experience for the live match and post-match recap, drawing on RLCS tournament aesthetics
- Preserve the existing v1 single-EXE deployment story
- Reuse the existing `StatsEventBus` as the architectural seam — the web tier is a peer consumer alongside the console renderer and JSONL logger
- Keep persistence simple: JSONL files are the source of truth; no database for v1
- Allow the dashboard to be reached from any device on the home LAN (phone, tablet, smart TV) without auth — trusted-LAN assumption

## 3. Non-goals (v1)

These are intentionally cut. The architecture leaves doors open for most of them; v1 simply doesn't ship them.

1. No career / cross-match aggregation (no win-rate over time, no head-to-head, no trends)
2. No SQL persistence — JSONL is canonical; no SQLite/SQL Server in v1
3. No replay scrubber (event-by-event time travel through a match)
4. No ball/player minimap; snapshot positional fields are not broadcast in v1
5. No Discord broadcaster — distinct v2 effort
6. No mobile/tablet polish — functional only; layout assumes desktop or TV
7. No internationalization — English only
8. No theme switcher — RLCS theme is the only theme
9. No audio — silent dashboard
10. No authentication / public hosting — trusted-LAN assumption
11. No video or `.replay` file parsing
12. No automated visual-regression testing (manual visual review for v1)
13. No OpenTelemetry traces export — local Serilog only
14. No auto-transition from live → recap on `MatchEndedEvent` — user navigates explicitly (training mode often follows a match instantly; auto-switching would feel like the dashboard is fighting the user)

## 4. Decisions log

| # | Decision | Why |
|---|---|---|
| D1 | Dual-mode views (live + recap) with **manual navigation** between them | Auto-transitioning on match end conflicts with how RL sessions actually flow (training/free play often immediately follows a match). User chooses when to view recap. |
| D2 | Visual aesthetic: **RLCS broadcast primary** (bold blue/orange, sharp angular panels, neon glow) + **TV broadcast cinematic transitions** (lower-third goal overlays, scene-style recap intros) | User-chosen direction. RLCS is the most recognizable Rocket League visual language; cinematic transitions add the "wow" moment without being constant. |
| D3 | Deployment: **bind to `0.0.0.0` by default** so the dashboard is reachable from any device on the home LAN | User opted for LAN access. Trusted-network assumption; no auth in v1. |
| D4 | History scope: **all matches captured, filtered at presentation layer**; default filter "Online only", toggle to include training/free play | User-chosen direction. Capture-vs-show separation prevents data loss; filter is one chip on the history page. |
| D5 | Audio: **silent dashboard** | User choice. Game audio plays through speakers; dashboard is a visual companion. |
| D6 | Personalization: **`/settings` page** to enter in-game name; powers own-card highlight, win/loss tagging on history, "you" tagging on MVP | User-chosen. No auto-detection (unreliable). No personalization at all (option A) loses too much value. |
| D7 | Branding: **generic but RL-flavored** — RL color language and aesthetic, no RL trademarks/logos/font names | User choice. Safer if ever shared; design language carries the identity. |
| D8 | Project structure: **dedicated `RocketLeagueStats.Web` .NET project + sibling `RocketLeagueStats.WebApp` Angular workspace**, both in `src/`; `Console` project remains the entry point and composes both | Clean modular layout chosen for clarity. Preserves single-EXE story while keeping concerns separated — Console owns terminal UX, Web owns browser-facing concerns, Core remains the engine. Angular workspace lives under `src/` for consistency with .NET projects. |
| D9 | Real-time transport: **SignalR with strongly-typed `Hub<IStatsHubClient>`** | First-class .NET; auto-reconnect; clean Angular client (`@microsoft/signalr`); fallbacks to SSE/long-poll if WebSockets fail. Strong typing prevents method-name typos at compile time. |
| D10 | No global broadcast throttle. **Per-method cadence** matches data nature: `OnGoal`/`OnStatfeed`/lifecycle events broadcast as-is (bursty); `OnClockTick` at 1 Hz on integer-second change; `OnPlayerStatsTick` only on change | Bandwidth isn't the constraint on a LAN. Throttling at the broadcast layer is solving the wrong problem. The bus stays 30 Hz for any future consumer (replay scrubber, minimap); the projector decides what to broadcast and at what rate based on what the data represents. |
| D11 | Persistence (v1): **JSONL replay → in-memory match index**; settings file at `%APPDATA%/RocketLeagueStats/settings.json`. No DB. | JSONL is already the canonical event store. Adding SQLite would duplicate state with sync risk. Match index for ~1000 matches ≈ 40MB working memory — well within budget. |
| D12 | REST endpoints under `/api/`, no version prefix. SignalR hub at `/hub/stats` | Local-only app; semantic versioning lives in the release tag, not the URL. Cleaner URLs. |
| D13 | Hub is **broadcast-only**; cold-load via HTTP `GET /api/state` | Initial state load via REST is simpler than a hub round-trip — caches/inspects in DevTools, doesn't depend on hub state. SignalR is a pure broadcast pipe. |
| D14 | Landing page is a **chooser**, not an auto-router. Live tile shows live state if active, otherwise an inactive tile. History tile always available. | User wants to decide whether to view live or recaps; no automatic routing — same rationale as D1. |
| D15 | Goal overlay is **scoped to `LiveViewComponent`**; match-end toast is **app-wide**. | Goal overlays shouldn't fire on settings/history pages (intrusive). Match-end toast IS cross-route because it offers navigation to the just-ended recap. |

## 5. Architecture

### 5.1 Project layout

```
E:/Source/RocketLeagueStats/
├── RocketLeagueStats.slnx
├── global.json
├── Directory.Build.props
├── Directory.Build.rsp
├── Directory.Packages.props
├── src/
│   ├── RocketLeagueStats.Core/                  (existing — unchanged scope)
│   │   ├── Events/
│   │   ├── Bus/StatsEventBus.cs
│   │   ├── Connection/StatsApiClient.cs
│   │   └── Persistence/                          (JSONL writer)
│   │
│   ├── RocketLeagueStats.Console/                (existing — entry point, composes Core + Web)
│   │   ├── Program.cs                            ← switches to WebApplication.CreateBuilder
│   │   ├── HostedServices/ConsoleRendererService.cs
│   │   ├── Rendering/EventFormatter.cs
│   │   └── Settings/SettingsStore.cs             ← NEW: %APPDATA% read/write
│   │
│   ├── RocketLeagueStats.Web/                    ← NEW
│   │   ├── DependencyInjection/
│   │   │   └── WebServiceCollectionExtensions.cs (AddRocketLeagueStatsWeb)
│   │   ├── Hubs/StatsHub.cs                      (Hub<IStatsHubClient>)
│   │   ├── Hubs/IStatsHubClient.cs               (typed broadcast contract)
│   │   ├── Endpoints/MatchesEndpoints.cs         (Minimal API mediated)
│   │   ├── Endpoints/SettingsEndpoints.cs
│   │   ├── Endpoints/StateEndpoints.cs
│   │   ├── Endpoints/InfoEndpoints.cs
│   │   ├── Services/MatchHistoryIndex.cs         (JSONL replay → in-memory)
│   │   ├── Services/LiveMatchProjector.cs        (subscribes to bus, computes running state, broadcasts via hub)
│   │   ├── Services/MatchTypeClassifier.cs       (parses MatchStateSnapshot.RawData → MatchType)
│   │   ├── Contracts/                            (DTOs)
│   │   ├── Mediator/                             (queries + handlers)
│   │   └── wwwroot/                              ← Angular publish output (gitignored, generated)
│   │
│   └── RocketLeagueStats.WebApp/                 ← NEW (Angular workspace)
│       ├── angular.json
│       ├── package.json
│       ├── tsconfig.json
│       ├── tailwind.config.ts
│       ├── public/favicon.svg
│       └── src/                                  (Angular source, see §7)
│
├── tests/
│   ├── RocketLeagueStats.Core.Tests/             (existing, 26 tests)
│   ├── RocketLeagueStats.Web.Tests/              ← NEW
│   └── RocketLeagueStats.WebApp.E2E/             ← NEW (Playwright)
│
├── samples/http/
│   ├── Matches.http                              ← NEW
│   ├── Settings.http                             ← NEW
│   └── State.http                                ← NEW
│
├── tools/
│   ├── Build-Solution.ps1                        (existing; updated to call Build-WebApp first)
│   ├── Build-WebApp.ps1                          ← NEW
│   └── Publish-Release.ps1                       (existing self-contained build, updated)
│
└── docs/superpowers/specs/2026-05-01-angular-frontend-design.md   (this file)
```

### 5.2 Deployment topology

A single `RocketLeagueStats.Console.exe` process owns:

```
                          ┌─────────────────────────────────────────┐
   Rocket League ──TCP───►│  StatsApiClient    (Core)               │
   :49123                 │       │                                 │
                          │       ▼                                 │
                          │  StatsEventBus (Channel<StatsEvent>)    │
                          │       │ │ │ │                           │
                          │       │ │ │ └──► ConsoleRendererService │ → terminal
                          │       │ │ └────► JsonlLoggerService     │ → logs/*.jsonl
                          │       │ └──────► LiveMatchProjector     │ → SignalR hub → all browser clients
                          │       └────────► MatchHistoryIndex      │ → in-memory match index
                          │                                         │
                          │  Kestrel HTTP server (0.0.0.0:5000)     │
                          │  ├── /hub/stats        (SignalR)        │ ◄── Angular SPA hub client
                          │  ├── /api/matches      (REST)           │ ◄── Angular history view
                          │  ├── /api/matches/{id} (REST)           │ ◄── Angular recap view
                          │  ├── /api/state        (REST)           │ ◄── Angular live view init
                          │  ├── /api/settings     (REST)           │ ◄── Angular settings page
                          │  ├── /api/info         (REST)           │
                          │  ├── /health           (REST)           │
                          │  └── wwwroot/          (static)         │ ◄── Angular index.html + assets
                          └─────────────────────────────────────────┘
                                          ▲
                                          │ http://<gaming-pc>:5000
                          ┌───────────────┴───────────────┐
                          │ Browser (any device on LAN)   │
                          │   Angular SPA                  │
                          └────────────────────────────────┘
```

**Single listener constraint** (load-bearing): Rocket League's Stats API allows only one TCP listener per game instance. Therefore the console + web cannot run as separate processes; they must share one process that holds the connection.

### 5.3 Event data flow

**Discrete events** (goals, statfeed, lifecycle):

1. `StatsApiClient` parses TCP frame → publishes typed event to `StatsEventBus`.
2. `LiveMatchProjector` (Web project, subscriber) receives event → updates running match state → broadcasts a typed DTO via `StatsHub` to all connected clients.
3. Angular hub client receives DTO → updates the relevant signal in `LiveMatchStore` → views re-render.

**Continuous snapshot** (`MatchStateSnapshot`, ~30/sec):

1. `StatsApiClient` publishes snapshot to bus.
2. `LiveMatchProjector` consumes snapshots **but does not broadcast them**. Instead, it extracts UI-relevant fields and broadcasts purpose-shaped methods at appropriate rates:
   - `OnClockTick(int seconds)` — broadcast only when the integer-seconds value changes (≤ 1 Hz)
3. The bus stays 30 Hz for future consumers (replay scrubber, minimap, etc.). Nothing is lost on disk — JSONL captures every snapshot.

**Match lifecycle (history index update)**:

1. `MatchInitializedEvent` → `MatchHistoryIndex.beginMatch(matchId, header)`.
2. All in-match events → `MatchHistoryIndex.append(matchId, event)`.
3. `MatchEndedEvent` → `MatchHistoryIndex.completeMatch(matchId, summary)`; emits `OnMatchEnded` over the hub so any open browser pops a "View Recap" toast.

### 5.4 Startup sequence (cold start of EXE)

1. Read `%APPDATA%/RocketLeagueStats/settings.json` (player name, prefs).
2. Build `WebApplication`, register Core + Web + Console services.
3. `MatchHistoryIndex` scans `logs/*.jsonl(.gz)` to build the in-memory match index (background worker, doesn't block startup; UI shows "loading history" until ready).
4. Kestrel binds `0.0.0.0:5000` (configurable via `--web-port` or `RLS_WEB_PORT` env var).
5. `StatsApiClient` opens TCP connection to RL.
6. `ConsoleRendererService` starts terminal output (existing behavior preserved).
7. Browser clients connect to `/hub/stats` whenever a user opens the URL.

## 6. API contract

### 6.1 Wire conventions

- All timestamps **UTC ISO-8601** strings (`2026-05-01T14:02:41.123Z`); the frontend converts to local
- `matchClockSeconds` is **integer seconds elapsed** since match start (normalized at the API regardless of how RL surfaces the clock)
- `team` strings: `"blue"` | `"orange"` (mapped from `TeamNum` 0/1 at the boundary)
- JSON casing: **camelCase** wire / PascalCase C# (`JsonNamingPolicy.CamelCase`)
- `goalSpeedUuPerSec` keeps RL's native unit (Unreal Units per second); UI converts to km/h for display
- All match IDs are **UUIDs minted by the API** when `MatchInitializedEvent` fires
- API root path: **`/api/`** (no `/v1`)

### 6.2 SignalR hub — `/hub/stats`

```csharp
public interface IStatsHubClient
{
    Task OnGoal(GoalDto goal);
    Task OnStatfeed(StatfeedDto statfeed);
    Task OnMatchInitialized(MatchHeaderDto header);
    Task OnMatchEnded(MatchSummaryDto summary);
    Task OnClockTick(int matchClockSeconds);            // 1 Hz max, only on int-second change
    Task OnPlayerStatsTick(PlayerStatsRowDto[] rows);    // on change only
    Task OnConnectionState(ConnectionStateDto state);    // RL TCP up/down
    Task OnPhaseChanged(MatchPhase phase);               // "idle" | "live"
}

public sealed class StatsHub : Hub<IStatsHubClient>
{
    // No client→server methods in v1.
}
```

### 6.3 REST endpoints (Minimal API + Mediator)

| Method | Path | Mediator query | Returns |
|---|---|---|---|
| `GET` | `/api/state` | `GetLiveStateQuery` | `LiveStateDto` — cold-load snapshot for the live view |
| `GET` | `/api/matches` | `GetMatchHistoryQuery(types[], from?, to?, sort)` | `MatchSummaryDto[]` — filtered history list |
| `GET` | `/api/matches/{id}` | `GetMatchRecapQuery(id)` | `MatchRecapDto` — full recap data |
| `GET` | `/api/settings` | `GetSettingsQuery` | `SettingsDto` |
| `PUT` | `/api/settings` | `UpdateSettingsCommand(SettingsDto)` | `SettingsDto` (echoes saved state) |
| `GET` | `/api/info` | `GetInfoQuery` | `ServerInfoDto` — version, build date, capabilities |
| `GET` | `/health` | (Microsoft health checks middleware) | health JSON |

`.http` samples in `samples/http/` cover success + 404/400 cases for each endpoint.

### 6.4 DTOs

```csharp
public sealed record PlayerRefDto(
    string Name,
    int Shortcut,           // RL's stable int per player-in-match
    string Team);           // "blue" | "orange"

public sealed record Vec3Dto(double X, double Y, double Z);

public sealed record GoalDto(
    string Id,                          // UUID
    DateTime Timestamp,
    int MatchClockSeconds,
    PlayerRefDto Scorer,
    PlayerRefDto? Assister,
    double GoalSpeedUuPerSec,
    Vec3Dto ImpactLocation,
    int BlueScoreAfter,
    int OrangeScoreAfter,
    int? SecondsSinceLastGoal);         // null for first goal of match

public sealed record StatfeedDto(
    DateTime Timestamp,
    int MatchClockSeconds,
    StatfeedType Type,
    PlayerRefDto MainTarget,
    PlayerRefDto? SecondaryTarget);

public sealed record MatchHeaderDto(
    string MatchId,
    DateTime StartedAt,
    MatchType Type,
    string PlaylistRaw,
    PlayerRefDto[] BluePlayers,
    PlayerRefDto[] OrangePlayers,
    string? ArenaName);

public sealed record PlayerStatsRowDto(
    PlayerRefDto Player,
    int Goals,
    int Assists,
    int Saves,
    int EpicSaves,
    int Shots,
    int DemosInflicted,
    int DemosTaken,
    int CrossbarHits,
    double FastestGoalSpeedUuPerSec,
    double MvpScore,                    // goals*3 + assists*2 + saves*1.5 + shots*0.5 + epicSaves*2 - demosTaken*0.5
    bool IsMvp);                        // true only on the recap view; always false during live

public sealed record MatchSummaryDto(
    string MatchId,
    DateTime StartedAt,
    DateTime EndedAt,
    int DurationSeconds,
    MatchType Type,
    int BlueScore,
    int OrangeScore,
    PlayerRefDto[] AllPlayers,
    PlayerRefDto? Mvp,
    int TotalGoals,
    GoalDto? FastestGoal);

public sealed record MatchRecapDto(
    MatchSummaryDto Summary,
    GoalDto[] Goals,
    StatfeedDto[] Statfeeds,
    PlayerStatsRowDto[] PlayerStats,    // sorted: MVP first, then by score desc
    int[] TimeBetweenGoalsSeconds,
    GameFlowDto Flow);

public sealed record GameFlowDto(
    int[] TimestampSeconds,             // x-axis
    int[] BlueScoreAtStep,
    int[] OrangeScoreAtStep);

public sealed record LiveStateDto(
    MatchPhase Phase,                   // "idle" | "live"
    MatchHeaderDto? CurrentMatch,
    int? CurrentMatchClockSeconds,
    int BlueScore,
    int OrangeScore,
    PlayerStatsRowDto[] PlayerStats,
    GoalDto[] RecentGoals,              // last 8, newest first
    StatfeedDto[] RecentStatfeeds,      // last 8, newest first
    DateTime? LastGoalAt,
    ConnectionStateDto Connection);

public sealed record ConnectionStateDto(
    bool ConnectedToGame,
    DateTime? LastEventReceivedAt);

public sealed record SettingsDto(
    string? PlayerName,
    string[] FriendNames,
    bool ShowTrainingInHistory);

public sealed record ServerInfoDto(
    string Version,
    DateTime BuildDate,
    string[] EnabledFeatures);

public enum MatchPhase { Idle, Live }
public enum MatchType { Ranked1v1, Ranked2v2, Ranked3v3, Casual, Tournament, Private, FreePlay, Training, Unknown }
public enum StatfeedType { Save, EpicSave, Demolish, Hattrick, MvpHattrick, Other }
```

### 6.5 Cold-load flow

1. SPA boots, `app.config.ts` provides `HttpClient` and the hub client.
2. `LiveMatchStore` constructor:
   - Connects SignalR hub at `/hub/stats` (auto-reconnect: 0/2/10/30s, then every 30s)
   - In parallel, fires `GET /api/state` to seed initial signals
3. Once both resolve, the live view renders. Subsequent updates flow only through the hub.
4. **On hub reconnect**, the store re-fires `GET /api/state` to recover from any missed events (background recovery).

## 7. Angular app structure

### 7.1 Routes

```typescript
// app.routes.ts
export const routes: Routes = [
  { path: '',               component: LandingPageComponent,  title: 'Rocket League Stats' },
  { path: 'live',           component: LiveViewComponent,     title: 'Live Match' },
  { path: 'history',        component: HistoryViewComponent,  title: 'Match History' },
  { path: 'recap/:matchId', component: RecapViewComponent,    title: 'Match Recap' },
  { path: 'settings',       component: SettingsPageComponent, title: 'Settings' },
  { path: '**', redirectTo: '' },
];
```

No guards, no resolvers, no auto-redirects. The user navigates explicitly.

### 7.2 File layout

```
src/RocketLeagueStats.WebApp/src/
├── main.ts
├── index.html
├── styles.css
├── styles/
│   ├── tokens.css          (CSS custom properties — palette, typography scale)
│   └── animations.css      (keyframes — goal-flash, pulse, slide-in)
└── app/
    ├── app.config.ts       (provideRouter, provideHttpClient, provideAnimationsAsync)
    ├── app.routes.ts
    ├── app.component.ts    (shell: nav + connection banner + match-end toast outlet + router-outlet)
    │
    ├── core/
    │   ├── api/
    │   │   ├── api-client.service.ts
    │   │   └── stats-hub.client.ts
    │   ├── state/
    │   │   ├── live-match.store.ts     (signals: phase, header, score, clock, players, recentEvents, lastGoalAt, connection)
    │   │   ├── history.store.ts        (filter signal + matches resource())
    │   │   ├── recap.store.ts          (currentRecapId signal + recap resource())
    │   │   ├── settings.store.ts
    │   │   └── toast.store.ts
    │   └── models/                     (TypeScript types matching API DTOs)
    │
    ├── features/
    │   ├── landing/   (landing-page, live-tile, history-tile)
    │   ├── live/      (live-view, scoreboard-header, time-since-goal, action-feed, action-feed-item, player-card, goal-overlay)
    │   ├── history/   (history-view, filter-bar, summary-strip, match-card)
    │   ├── recap/     (recap-view, hero-section, goal-timeline, time-between-goals.chart, player-stats-table, speed-leaderboard, game-flow.chart)
    │   └── settings/  (settings-page)
    │
    └── shared/
        ├── components/
        │   ├── nav-bar.component.ts
        │   ├── connection-banner.component.ts
        │   ├── match-end-toast.component.ts
        │   ├── panel.component.ts                 (the angular-cut panel primitive)
        │   ├── team-stripe.component.ts
        │   ├── match-type-badge.component.ts
        │   └── player-name.component.ts           (initials chip + name; highlights "you")
        └── pipes/
            ├── kmh.pipe.ts                        (UU/s → km/h)
            ├── duration.pipe.ts                   (seconds → "5:42")
            └── relative-time.pipe.ts              (Date → "2 minutes ago")
```

All components are **standalone** (no NgModules). Angular 20+ default.

### 7.3 State stores — signal-first

`LiveMatchStore` is the central live-state store, `providedIn: 'root'`. Holds writable signals for everything the live view consumes; bootstraps the hub connection and the initial `/api/state` fetch in its constructor.

`HistoryStore` and `RecapStore` use Angular's `resource()` API for declarative async loading — request/response cycles tied to a request signal, with built-in `value` / `isLoading` / `error` / `status` signals. No Observables for fetches; Observables only for the SignalR stream.

`SettingsStore` loads settings once on construction, exposes a writable signal, persists via `PUT /api/settings` on save.

`ToastStore` holds the latest match-end toast state (a `MatchSummaryDto | null` signal that auto-clears after 30s).

### 7.4 App shell

```typescript
@Component({
  selector: 'rls-root',
  imports: [RouterOutlet, NavBarComponent, ConnectionBannerComponent, MatchEndToastComponent],
  template: `
    <rls-nav-bar />
    <rls-connection-banner />
    <main class="app-content">
      <router-outlet />
    </main>
    <rls-match-end-toast />
  `,
})
export class AppComponent { }
```

`<rls-goal-overlay />` is mounted **inside `LiveViewComponent`** only (not the app shell) — overlays should not fire on history/settings pages.

### 7.5 Hub client wrapper

Thin façade over `@microsoft/signalr`'s `HubConnectionBuilder`. Exposes typed `onGoal()`, `onStatfeed()`, etc. methods that take callbacks. Manages connection lifecycle and `state` signal (`'idle' | 'connecting' | 'connected' | 'reconnecting' | 'disconnected'`). On reconnect, fires registered `onReconnected` callbacks (the live store uses this to refetch `/api/state`).

Reconnect policy: `withAutomaticReconnect([0, 2_000, 10_000, 30_000])` — retries at 0s, 2s, 10s, 30s, then keeps trying every 30s.

### 7.6 Library choices

| Concern | Library | Notes |
|---|---|---|
| Angular | **Angular 20 LTS** (or latest stable at implementation time) | Standalone components, signals, new control flow (`@if`, `@for`), `resource()` API |
| Real-time client | **`@microsoft/signalr`** | Official SignalR JS client; pairs with `Hub<IStatsHubClient>` server-side |
| Styling | **Tailwind CSS v4** (CSS-based config) + custom CSS for clip-path panels | Utility-first; tokens (palette, typography) live in `styles/tokens.css`, not in the Tailwind theme |
| Charts | **Apache ECharts** via `ngx-echarts` | Best-in-class for time-series + bar charts; theme-able to RLCS palette; loaded lazily on `/recap/:matchId` only |
| Animations | **`@angular/animations`** + native View Transitions API | Used for goal lower-third, scene transitions; no GSAP dependency |
| Unit testing | **Vitest** + **Angular Testing Library** | Vitest is faster than Karma, fully supported by Angular 20+ |
| E2E testing | **Playwright** | Same tool used for Blazor; supports CI caching via `dotnet-skills:playwright-ci-caching` |

| .NET concern | Library | Notes |
|---|---|---|
| Mediator | **`martinothamar/Mediator`** | Per CLAUDE.md (not MediatR) |
| Logging | **Serilog** | Existing in v1 console; reused |
| Health checks | **`Microsoft.Extensions.Diagnostics.HealthChecks`** | Standard ASP.NET Core middleware |
| OpenAPI (optional but recommended) | **`Microsoft.AspNetCore.OpenApi`** | Generates Swagger at build time; powers `.http` samples |
| Testing | **xUnit + NSubstitute** | Per CLAUDE.md |
| Integration testing | **`Microsoft.AspNetCore.Mvc.Testing`** (`WebApplicationFactory`) | In-process API spin-up |

### 7.7 Dev vs prod

- **Dev**: `ng serve --proxy-config proxy.conf.json` runs Angular on `:4200`, proxies `/api/*` and `/hub/*` to `http://localhost:5000`. The .NET backend runs separately (`dotnet run` from `RocketLeagueStats.Console`). CORS configured to allow `http://localhost:4200` only when `ASPNETCORE_ENVIRONMENT=Development`.
- **Prod**: `Build-WebApp.ps1` builds the Angular bundle → copies to `src/RocketLeagueStats.Web/wwwroot/`. The .NET project serves the SPA via `app.UseStaticFiles() + app.MapFallbackToFile("index.html")`. Single EXE, single port (5000), same-origin everything.

## 8. Visual design language

### 8.1 Color palette (`styles/tokens.css`)

```css
:root {
  --bg-base: #07090F;
  --bg-elevated: #13182A;
  --bg-overlay: #0A0E1Acc;

  --team-blue: #00B7FF;
  --team-blue-deep: #003D55;
  --team-blue-glow: rgba(0, 183, 255, 0.45);
  --team-orange: #FF8500;
  --team-orange-deep: #5A2E00;
  --team-orange-glow: rgba(255, 133, 0, 0.45);

  --accent-mvp: #FFC107;
  --accent-mvp-glow: rgba(255, 193, 7, 0.55);
  --accent-success: #00E676;
  --accent-danger: #FF3D5A;
  --accent-cyan: #00E5FF;

  --text-primary: #F0F4FF;
  --text-secondary: #7A8AA8;
  --text-muted: #4A5A78;

  --shadow-panel: 0 4px 24px rgba(0, 0, 0, 0.6);
  --shadow-glow-blue: 0 0 32px var(--team-blue-glow);
  --shadow-glow-orange: 0 0 32px var(--team-orange-glow);
  --shadow-glow-mvp: 0 0 32px var(--accent-mvp-glow);
}
```

### 8.2 Typography

| Family | Weights | Use |
|---|---|---|
| **Bebas Neue** | 400 | Display: scoreboard digits, "GOAL" overlay, big headers |
| **Rajdhani** | 500/600/700 | Section headings, panel titles, badges |
| **Inter** | 400/500/600 | Body, table data, labels |

Tabular figures (`font-feature-settings: 'tnum' 1, 'lnum' 1`) on every numeric surface that updates live.

### 8.3 Angular-cut panel — signature primitive

A single Angular `<rls-panel>` component backed by CSS `clip-path`, with a `team` input (`'blue' | 'orange' | 'neutral' | 'mvp'`) driving the accent color. Two layered pseudo-elements (`::before` border, `::after` surface inset by border thickness) create a clipped border without leaking. Glows use `filter: drop-shadow()` (which respects `clip-path`), not `box-shadow` (which would leak outside the clipped silhouette).

```css
.panel {
  --panel-cut: 14px;
  --panel-bg: var(--bg-elevated);
  --panel-border: var(--accent-cyan);
  --panel-border-thickness: 2px;

  position: relative;
  isolation: isolate;
  padding: 1.25rem 1.5rem;
}
.panel::before {
  content: '';
  position: absolute;
  inset: 0;
  background: var(--panel-border);
  clip-path: polygon(
    var(--panel-cut) 0, 100% 0,
    100% calc(100% - var(--panel-cut)), calc(100% - var(--panel-cut)) 100%,
    0 100%, 0 var(--panel-cut)
  );
  z-index: -2;
}
.panel::after {
  content: '';
  position: absolute;
  inset: var(--panel-border-thickness);
  background: var(--panel-bg);
  clip-path: polygon(
    var(--panel-cut) 0, 100% 0,
    100% calc(100% - var(--panel-cut)), calc(100% - var(--panel-cut)) 100%,
    0 100%, 0 var(--panel-cut)
  );
  z-index: -1;
}
.panel--blue   { --panel-border: var(--team-blue); }
.panel--orange { --panel-border: var(--team-orange); }
.panel--mvp    { --panel-border: var(--accent-mvp); --panel-border-thickness: 3px; }
```

### 8.4 Motion principles

| Tier | Trigger | Duration | Easing | Examples |
|---|---|---|---|---|
| **Ambient** | Always-on | continuous | linear / ease | live-indicator pulse, connection LED breathe, ticker scroll |
| **Reactive** | UI interaction or data change | 200–400ms | `cubic-bezier(0.4, 0, 0.2, 1)` | route change fade, score flash, action feed slide-in, hover glows |
| **Cinematic** | Discrete game event | 3000–4000ms | in: `cubic-bezier(0.16, 1, 0.3, 1)` / out: `cubic-bezier(0.7, 0, 0.84, 0)` | goal overlay, match-end toast, recap hero entrance |

`@media (prefers-reduced-motion: reduce)` collapses reactive/cinematic timings to ≤100ms cross-fades; ambient pulses freeze.

### 8.5 Goal overlay timeline (~3.6s)

```
t=0       slide in from y+40px, opacity 0→1, scale 0.96→1.00       (320ms, cinematic-in)
t=320     team-color stripe pulse ×2 (1.0→1.08→1.0)                 (~600ms each)
t=320     scorer name fades in                                       (200ms ease)
t=520     assister name fades in                                     (200ms ease)
t=720     speed counter rolls 0 → final value                        (400ms ease-out)
t=3200    hold                                                       (—)
t=3200    opacity 1→0, slide y+20px                                  (320ms, cinematic-out)
t=3520    pendingGoalOverlay = null
```

Layout: lower-third, ~25vh × 100vw, team-color stripe on left edge, "GOAL" + scorer + assister + speed.

If a second goal arrives while overlay is visible, the first cross-fades out fast (200ms) and the second takes over. Not a queue — only one overlay at a time.

### 8.6 Action feed item language

| Event type | Icon | Text pattern | Edge color |
|---|---|---|---|
| Goal | ⬢ | `{Scorer}` scored! `(assist {Assister})` | team |
| Save | ◯ | `{Player}` made a save | team |
| Epic Save | ✦ | `{Player}` made an EPIC save | team + gold inner glow |
| Demolish | ✕ | `{Aggressor}` demolished `{Victim}` | aggressor team |
| Crossbar | ▱ | `{Player}` hit the crossbar | team |

Container holds 8 items max; older items fade-out + collapse over 600ms.

### 8.7 Match-end toast

App-wide, top-right, ~360px wide. Trapezoid panel with gold accent. Slides in from `+x` 32px → 0 over 300ms (cinematic-in). Auto-dismisses after 30s; user can dismiss earlier or click "View Recap" → routes to `/recap/{id}` and dismisses.

### 8.8 Connection banner

Slim 32px-tall strip at top of viewport. Conditional:

| State | Color | Text |
|---|---|---|
| `!gameConnected` | warm amber | `Disconnected from Rocket League — waiting for game...` |
| `!hubConnected` | red | `Reconnecting to server...` (with spinning indicator) |
| Both OK | hidden | — |

Slides down 200ms reactive-in. Banner stays in flow (the layout doesn't jump under it).

### 8.9 Responsive strategy

| Mode | Trigger | Adjustments |
|---|---|---|
| **Desktop** | default (1280–2560px) | Three-column live layout (feed center, players left/right) |
| **TV** | `?display=tv` query param | Font sizes ×1.4, hide non-essential metadata, larger touch/hover targets, 10-foot-UI safe |
| **Mobile/tablet** | `< 768px` | Single-column stack, condensed action feed, no goal overlay (too disruptive on small screens), recap charts scroll horizontally |

Mobile is **functional, not optimized** for v1.

### 8.10 Reference layouts

#### Live view

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ NAV BAR                                                                     │
├─────────────────────────────────────────────────────────────────────────────┤
│ CONNECTION BANNER (when needed)                                             │
├─────────────────────────────────────────────────────────────────────────────┤
│  ┌──────────────────── SCOREBOARD HEADER ────────────────────┐              │
│  │  CASUAL 2v2                                       4:32     │              │
│  │  ▍ BLUE       4              —              2  ORANGE ▍   │              │
│  └────────────────────────────────────────────────────────────┘             │
│                                                                              │
│  ┌────────────────────┐   ┌──────────────────┐   ┌────────────────────┐    │
│  │ BLUE PLAYER CARD   │   │ ACTION FEED      │   │ ORANGE PLAYER CARD │    │
│  │ Hellcat            │   │  ⬢ goal          │   │ Stinkmaster         │    │
│  │ G 2 A 1 Sv 3       │   │  ◯ save          │   │ G 1 A 0 Sv 4        │    │
│  └────────────────────┘   └──────────────────┘   └────────────────────┘    │
│                                                                              │
│  ┌────────────── TIME SINCE LAST GOAL ─────────────┐                        │
│  │              0 : 47                              │                        │
│  └──────────────────────────────────────────────────┘                       │
└─────────────────────────────────────────────────────────────────────────────┘
```

#### Recap view

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ HERO                                                                         │
│  CASUAL 2v2  ·  4:32 duration                                                │
│  ▍ BLUE 4 — 2 ORANGE ▍                                                      │
│  [MVP CARD: Hellcat — 2G/1A/3Sv]                                            │
├─────────────────────────────────────────────────────────────────────────────┤
│  ┌────── GOAL TIMELINE ──────┐  ┌── TIME BETWEEN GOALS ──┐                  │
│  │ 0:00 1:30 2:45 3:10 4:20  │  │ 90s 75s 25s 70s 30s    │                 │
│  │  •●   ●    ○●   ●    ○    │  │ ▆ ▃ ▂ ▅ ▁              │                 │
│  └───────────────────────────┘  └────────────────────────┘                  │
├─────────────────────────────────────────────────────────────────────────────┤
│  ┌──── PLAYER STATS TABLE ────┐  ┌── SPEED LEADERBOARD ──┐                  │
│  │ name  G A Sv ES Sh D T  ⚡ │  │ 1. Hellcat 2104 UU/s   │                 │
│  │ Hellc 2 1 3  1  5 1 0 2104 │  │ 2. Stink   1856 UU/s   │                 │
│  └────────────────────────────┘  └────────────────────────┘                 │
├─────────────────────────────────────────────────────────────────────────────┤
│  ┌────────────── GAME FLOW (cumulative score) ──────────────────┐           │
│  │ 4 ┤                                              ━━━ Blue    │           │
│  │ 3 ┤                              ╭─               ────────   │           │
│  │ 2 ┤                     ╭────────╯       ┌──────  Orange ─── │           │
│  │ 1 ┤            ╭────────╯       ┌────────╯                   │           │
│  │ 0 ┴──────────────────────────────────────────────► time      │           │
│  └──────────────────────────────────────────────────────────────┘           │
└─────────────────────────────────────────────────────────────────────────────┘
```

## 9. Testing strategy

| Project | Test types | What gets tested |
|---|---|---|
| `RocketLeagueStats.Core.Tests` (existing) | xUnit unit | Existing 26 tests preserved |
| `RocketLeagueStats.Web.Tests` (new) | xUnit unit | `MatchHistoryIndex` JSONL replay & match boundary detection, `MatchTypeClassifier` playlist parsing, `LiveMatchProjector` event-to-DTO translation + per-player tally derivation, all Mediator handlers (with NSubstitute mocks) |
| `RocketLeagueStats.Web.Tests` | xUnit integration via `WebApplicationFactory` | End-to-end through the API: in-process SignalR client subscribes, synthetic events pushed into the bus, assert client receives expected DTOs in expected order |
| `RocketLeagueStats.WebApp` | Vitest unit | Pipes (kmh/duration/relative-time), components in isolation, stores against a faked hub client |
| `RocketLeagueStats.WebApp.E2E` (new) | Playwright | Cold-load landing renders; history filter chips work; recap charts mount; match-end toast → "View Recap" navigates; connection banner appears on hub disconnect |

No automated visual regression in v1 (manual review).

## 10. Error & edge handling

| Scenario | Behavior | Surface |
|---|---|---|
| RL not running at app start | API binds normally; `connectedToGame: false` | Connection banner: "Disconnected from Rocket League — waiting…" |
| RL TCP closes mid-match | Emit `OnConnectionState(false)`; clock freezes | Connection banner; live view stays with stale data |
| Port in use | Fail-fast with clear message | Console stderr; suggests `--web-port=N` |
| Hub disconnect (browser ↔ API) | Auto-reconnect at 0/2/10/30s; on reconnect, refetch `/api/state` | Connection banner: "Reconnecting to server…" |
| Settings file missing | Create with defaults | Silent; UI prompts on first settings page visit |
| Settings file corrupted | Back up bad file, recreate, log warning | Logged; UI uses defaults |
| JSONL log line corrupted | Skip line, log warning, continue | Logged; counted in `/api/info.parseSkippedLines` |
| Goal with `Assister == null` | Render "**Hellcat** scored — solo" | UI handles null assister |
| Player name with special chars | Pass through; Angular interpolation is XSS-safe by default | UI renders correctly |
| Match with 0 goals | `Mvp = null`, `FastestGoal = null`, empty `TimeBetweenGoalsSeconds`, `GameFlow = [(0,0,0)]` | Recap renders "No goals scored" placeholder |
| Recap requested for unknown match ID | API returns 404 | UI: "Match not found — return to history" |
| Match in progress at history fetch | Excluded (only completed matches) | History list reflects committed matches only |
| Two goals within 1s | First overlay cross-fades out fast (200ms); second takes over | UI: rapid sequence still legible |
| `MatchType = Unknown` | Treated as non-Online by default filter; tagged `UNKNOWN` in history | Logged; visible in history with badge |

## 11. Performance budgets

| Surface | Budget | Rationale |
|---|---|---|
| API cold start (TCP listening) | ≤ 2.0s | Including JSONL scan |
| Live event end-to-end (RL → browser DOM) | ≤ 200ms p99 | Local LAN; SignalR + signal write |
| Goal overlay first paint after `OnGoal` | ≤ 32ms | One frame at 30fps |
| Angular bundle (initial, gzipped) | ≤ 350 KB | Charts library lazy-loaded only on Recap route |
| Recap view full render (10-goal match) | ≤ 500ms | From route nav to all charts painted |
| Memory steady state | ≤ 100 MB | Single EXE, in-memory match index ~10MB for ~100 matches |

These are budgets, not targets. v1 testing checks them as smoke assertions; v2 may add proper benchmarks.

## 12. Build & release pipeline

```
1. ./tools/Build-WebApp.ps1
     → cd src/RocketLeagueStats.WebApp
     → npm ci
     → npx ng build --configuration production
     → Copy-Item dist/web/browser/* → ../RocketLeagueStats.Web/wwwroot/

2. dotnet build RocketLeagueStats.slnx -c Release

3. dotnet test                                          (xUnit, all .NET test projects)
4. cd src/RocketLeagueStats.WebApp && npm test          (Vitest)
5. (CI only) playwright test                            (E2E, separate stage)

6. dotnet publish src/RocketLeagueStats.Console
     -c Release -r win-x64 --no-self-contained
     → RocketLeagueStats-v{version}-win-x64.exe         (preserves single-EXE story)
```

A `BeforeBuild` MSBuild target in `RocketLeagueStats.Web.csproj` checks that `wwwroot/index.html` exists and fails fast with "Run Build-WebApp.ps1 first" otherwise. Prevents the ship-broken-bundle bug.

## 13. Documentation deliverables (with v1 release)

- `README.md` — new "Web Dashboard" section: how to launch, default URL, settings page, screenshots
- `samples/http/Matches.http`, `Settings.http`, `State.http` — request examples per CLAUDE.md
- `docs/architecture.md` — bus → projector → hub → SPA flow (Mermaid)
- `docs/api-contract.md` — every hub method + REST endpoint with example payloads
- API XML doc comments — surfaced as Swagger if `Microsoft.AspNetCore.OpenApi` is added (recommended; tiny effort, high value for `.http` samples)

## 14. Glossary

| Term | Meaning |
|---|---|
| **Bus** | The in-process `Channel<StatsEvent>` (`StatsEventBus`) that fans out events from the TCP listener to all subscribers |
| **Projector** | `LiveMatchProjector` — translates raw bus events into UI-shaped DTOs and broadcasts via the hub |
| **Match index** | `MatchHistoryIndex` — in-memory list of completed matches, built by replaying JSONL on startup |
| **Snapshot** | `MatchStateSnapshot` — the periodic ~30 PPS engine state event from the RL Stats API; in v1 only used for the match clock |
| **Lower-third** | Broadcast-TV term for an overlay graphic in the lower portion of the viewport (the goal overlay is one) |
| **MVP score** | `goals*3 + assists*2 + saves*1.5 + shots*0.5 + epicSaves*2 - demosTaken*0.5` |

## 15. Related artifacts

- v1 console app spec / state: `memory/project_v1_console_app_state.md`
- Existing TCP client: `src/RocketLeagueStats.Core/Connection/StatsApiClient.cs`
- Existing event bus: `src/RocketLeagueStats.Core/Bus/StatsEventBus.cs`
- Existing event types: `src/RocketLeagueStats.Core/Events/`
- Existing console renderer: `src/RocketLeagueStats.Console/HostedServices/ConsoleRendererService.cs`
- Existing JSONL writer: `src/RocketLeagueStats.Core/Persistence/`
