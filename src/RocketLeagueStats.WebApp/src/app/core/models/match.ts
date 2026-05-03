import { PlayerRef } from './player';
import { MatchType } from './enums';
import { Goal } from './goal';

/**
 * Team metadata extracted from the first MatchStateSnapshot of a match. Color values are
 * 6-digit hex without a leading `#` (e.g. `1873FF`) so the frontend can drop them straight
 * into CSS variables. Named `TeamMeta` (not `Team`) to avoid colliding with the
 * `Team` color-enum already exported from `./enums`.
 */
export interface TeamMeta {
  name: string;
  colorPrimary: string;
  colorSecondary: string;
}

export interface MatchHeader {
  matchId: string;
  startedAt: string;
  type: MatchType;
  playlistRaw: string;
  bluePlayers: PlayerRef[];
  orangePlayers: PlayerRef[];
  arenaName: string | null;
  // Populated once the first MatchStateSnapshot of a match arrives. Null between MatchInitialized
  // and the first snapshot tick — the live UI should fall back to the default blue/orange palette
  // during that window.
  blueTeam: TeamMeta | null;
  orangeTeam: TeamMeta | null;
}

export interface MatchSummary {
  matchId: string;
  startedAt: string;
  endedAt: string;
  durationSeconds: number;
  type: MatchType;
  blueScore: number;
  orangeScore: number;
  allPlayers: PlayerRef[];
  mvp: PlayerRef | null;
  totalGoals: number;
  fastestGoal: Goal | null;
  // Populated from the Matches row that the writer fills at MatchEnded with the latest
  // snapshot's team metadata. Null on rows persisted before migration
  // AddTeamMetadataAndPlayerStats — the recap UI should fall back to its default palette.
  blueTeam: TeamMeta | null;
  orangeTeam: TeamMeta | null;
  arenaName: string | null;
  // Mirrors the persisted Match.WinnerTeamNum — 0 = blue won, 1 = orange won, null on
  // abandoned matches without a MatchEnded event. Used by the history card / recap hero to
  // colour the configured player's win/loss stripe directly, without re-deriving the winner
  // from BlueScore vs OrangeScore (overtime can end the moment a goal lands).
  winnerTeamNum: number | null;
}

export interface GameFlow {
  timestampSeconds: number[];
  blueScoreAtStep: number[];
  orangeScoreAtStep: number[];
}

export interface MatchRecap {
  summary: MatchSummary;
  goals: Goal[];
  statfeeds: import('./statfeed').Statfeed[];
  playerStats: import('./player-stats').PlayerStatsRow[];
  timeBetweenGoalsSeconds: number[];
  flow: GameFlow;
}
