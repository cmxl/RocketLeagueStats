import { MatchPhase } from './enums';
import { MatchHeader } from './match';
import { PlayerStatsRow } from './player-stats';
import { Goal } from './goal';
import { Statfeed } from './statfeed';

export interface ConnectionState {
  connectedToGame: boolean;
  lastEventReceivedAt: string | null;
}

export interface LiveState {
  phase: MatchPhase;
  currentMatch: MatchHeader | null;
  currentMatchClockSeconds: number | null;
  blueScore: number;
  orangeScore: number;
  playerStats: PlayerStatsRow[];
  recentGoals: Goal[];
  recentStatfeeds: Statfeed[];
  lastGoalAt: string | null;
  connection: ConnectionState;
}
