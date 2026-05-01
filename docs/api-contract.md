# RocketLeagueStats — API Contract

> Authoritative source: `src/RocketLeagueStats.Web/Hubs/IStatsHubClient.cs` and
> `src/RocketLeagueStats.Web/Endpoints/*.cs`. See `samples/http/*.http` for
> ready-to-run request examples.

---

## REST endpoints

All endpoints are served from the same process as the SPA (default `http://localhost:5000`).
Responses use camelCase JSON serialised with `System.Text.Json`.

---

### `GET /api/state`

Returns the current live state. Used by the Angular `LiveMatchStore` on first
connect (and on every SignalR reconnect) to seed signals without waiting for
the next hub push.

**Response: `200 OK`**
```json
{
  "phase": "Idle",
  "currentMatch": null,
  "currentMatchClockSeconds": null,
  "blueScore": 0,
  "orangeScore": 0,
  "playerStats": [],
  "recentGoals": [],
  "recentStatfeeds": [],
  "lastGoalAt": null,
  "connection": {
    "connectedToGame": false,
    "lastEventReceivedAt": null
  }
}
```

`phase` is `"Idle"` or `"Live"`. When a match is in progress `currentMatch`
and `currentMatchClockSeconds` are populated.

---

### `GET /api/matches`

Returns the match history list. All query parameters are optional.

| Parameter | Type | Default | Description |
|---|---|---|---|
| `includeTraining` | `bool` | `false` | Include training-mode matches |
| `includeFreePlay` | `bool` | `false` | Include free-play sessions |
| `from` | `DateTime` | — | Only matches after this UTC timestamp |
| `to` | `DateTime` | — | Only matches before this UTC timestamp |
| `sort` | `string` | `mostRecent` | `mostRecent` or `highestScoring` |

**Response: `200 OK`** — array of `MatchSummaryDto`:
```json
[
  {
    "matchId": "00000000-0000-0000-0000-000000000001",
    "startedAt": "2026-05-01T14:02:14.000Z",
    "endedAt": "2026-05-01T14:07:12.000Z",
    "durationSeconds": 298,
    "type": "Online",
    "blueScore": 3,
    "orangeScore": 2,
    "allPlayers": [
      { "name": "Hellcat", "shortcut": 1, "team": "blue" },
      { "name": "Stinkmaster", "shortcut": 2, "team": "blue" },
      { "name": "Sub", "shortcut": 3, "team": "orange" }
    ],
    "mvp": { "name": "Hellcat", "shortcut": 1, "team": "blue" },
    "totalGoals": 5,
    "fastestGoal": {
      "id": "00000000-0000-0000-0000-000000000002",
      "timestamp": "2026-05-01T14:02:41.123Z",
      "matchClockSeconds": 132,
      "scorer": { "name": "Hellcat", "shortcut": 1, "team": "blue" },
      "assister": null,
      "goalSpeedUuPerSec": 2104.0,
      "impactLocation": { "x": 0.0, "y": -5120.0, "z": 230.0 },
      "blueScoreAfter": 1,
      "orangeScoreAfter": 0,
      "secondsSinceLastGoal": null
    }
  }
]
```

`type` values: `"Online"`, `"Casual"`, `"Tournament"`, `"Private"`, `"Training"`, `"FreePlay"`, `"Unknown"`.

---

### `GET /api/matches/{id}`

Returns full recap data for a completed match.

**Response: `200 OK`** — `MatchRecapDto`:
```json
{
  "summary": { "...": "see MatchSummaryDto above" },
  "goals": [ { "...": "see GoalDto below" } ],
  "statfeeds": [ { "...": "see StatfeedDto below" } ],
  "playerStats": [ { "...": "see PlayerStatsRowDto below" } ],
  "timeBetweenGoalsSeconds": [45, 62, 18, 90],
  "flow": {
    "timestampSeconds": [0, 45, 107, 125, 215],
    "blueScoreAtStep": [0, 1, 1, 2, 3],
    "orangeScoreAtStep": [0, 0, 1, 1, 2]
  }
}
```

**Response: `404 Not Found`** — when no match exists with that id.

---

### `GET /api/settings`

Returns persisted user settings, or defaults if none have been saved.

**Response: `200 OK`**
```json
{
  "playerName": null,
  "friendNames": [],
  "showTrainingInHistory": false
}
```

---

### `PUT /api/settings`

Persists user settings. Returns the saved value.

**Request body:**
```json
{
  "playerName": "Hellcat",
  "friendNames": ["Stinkmaster", "Sub"],
  "showTrainingInHistory": false
}
```

**Response: `200 OK`** — the saved `SettingsDto`.

---

### `GET /api/info`

Returns server build metadata.

**Response: `200 OK`**
```json
{
  "version": "1.1.0",
  "buildDate": "2026-05-01T00:00:00.000Z",
  "enabledFeatures": ["Web", "SignalR", "MatchHistory"]
}
```

---

### `GET /health`

ASP.NET Core health-check endpoint.

**Response: `200 OK`** — `Healthy` (plain text)

---

## SignalR hub `/hub/stats`

Server-to-client push methods only — clients do not call any hub methods. The
hub broadcasts to all connected clients. Connect with `@microsoft/signalr`
at `/hub/stats`; the SPA uses automatic reconnect with delays `[0, 2000, 10000, 30000]` ms.

On every reconnect the `LiveMatchStore` re-fetches `GET /api/state` to fill any
gap in missed events.

---

### `OnGoal(GoalDto)`

Fired when a goal is scored.

```json
{
  "id": "00000000-0000-0000-0000-000000000001",
  "timestamp": "2026-05-01T14:02:41.123Z",
  "matchClockSeconds": 132,
  "scorer": { "name": "Hellcat", "shortcut": 1, "team": "blue" },
  "assister": { "name": "Stinkmaster", "shortcut": 2, "team": "blue" },
  "goalSpeedUuPerSec": 2104.0,
  "impactLocation": { "x": 0.0, "y": -5120.0, "z": 230.0 },
  "blueScoreAfter": 1,
  "orangeScoreAfter": 0,
  "secondsSinceLastGoal": null
}
```

`assister` and `secondsSinceLastGoal` are nullable.

---

### `OnStatfeed(StatfeedDto)`

Fired for saves, demolitions, epic saves, hattricks, etc.

```json
{
  "timestamp": "2026-05-01T14:02:38.500Z",
  "matchClockSeconds": 129,
  "type": "Save",
  "mainTarget": { "name": "Stinkmaster", "shortcut": 2, "team": "blue" },
  "secondaryTarget": null
}
```

`type` enum values: `"Other"`, `"Save"`, `"EpicSave"`, `"Demolish"`, `"Hattrick"`, `"MvpHattrick"`.
`secondaryTarget` is nullable.

---

### `OnMatchInitialized(MatchHeaderDto)`

Fired once when a match starts. Followed by `OnPhaseChanged("Live")`.

```json
{
  "matchId": "00000000-0000-0000-0000-000000000001",
  "startedAt": "2026-05-01T14:02:14.000Z",
  "type": "Online",
  "playlistRaw": "1v1",
  "bluePlayers": [
    { "name": "Hellcat", "shortcut": 1, "team": "blue" }
  ],
  "orangePlayers": [
    { "name": "Sub", "shortcut": 3, "team": "orange" }
  ],
  "arenaName": "DFH Stadium"
}
```

`arenaName` is nullable.

---

### `OnMatchEnded(MatchSummaryDto)`

Fired once when a match ends. Followed by `OnPhaseChanged("Idle")`.

See the `MatchSummaryDto` shape in `GET /api/matches` above.

---

### `OnClockTick(int)`

Fired at most 1 Hz — only when the integer-seconds match clock changes.
Payload is the current `matchClockSeconds` value as a plain integer.

```json
132
```

---

### `OnPlayerStatsTick(PlayerStatsRowDto[])`

Broadcast only when at least one player's running tally changes. Contains the
full array of all players in the current match.

```json
[
  {
    "player": { "name": "Hellcat", "shortcut": 1, "team": "blue" },
    "goals": 1,
    "assists": 0,
    "saves": 0,
    "epicSaves": 0,
    "shots": 2,
    "demosInflicted": 0,
    "demosTaken": 0,
    "crossbarHits": 0,
    "fastestGoalSpeedUuPerSec": 2104.0,
    "mvpScore": 210.4,
    "isMvp": true
  }
]
```

---

### `OnConnectionState(ConnectionStateDto)`

Fired when the backend's connection to Rocket League's TCP Stats API changes.

```json
{
  "connectedToGame": true,
  "lastEventReceivedAt": "2026-05-01T14:02:41.500Z"
}
```

`lastEventReceivedAt` is nullable.

---

### `OnPhaseChanged(MatchPhase)`

Fired when the match phase transitions. Payload is a string enum value.

```json
"Live"
```

Values: `"Idle"` (no match in progress) or `"Live"` (match active).

---

## Canonical type definitions

For the full list of properties and their nullability see the source records in
`src/RocketLeagueStats.Web/Contracts/`. The JSON property names are camelCase
versions of the C# record parameter names.
