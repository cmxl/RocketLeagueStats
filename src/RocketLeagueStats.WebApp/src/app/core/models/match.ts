import { PlayerRef } from './player';
import { MatchType } from './enums';
import { Goal } from './goal';

export interface MatchHeader {
  matchId: string;
  startedAt: string;
  type: MatchType;
  playlistRaw: string;
  bluePlayers: PlayerRef[];
  orangePlayers: PlayerRef[];
  arenaName: string | null;
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
